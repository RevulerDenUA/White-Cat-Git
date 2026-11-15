using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

// Встановлення бурів зі схеми, збір ресурсів машиною, сховище, збереження й офлайн-видобуток.
//   B     — показати/сховати схему бура перед машиною
//   ЛКМ / Enter — поставити бур (схема має бути синя, червона = не можна)
//   G     — забрати ресурси з найближчого бура
//   X (утримувати) — розібрати бур: його ресурси + вартість повертаються у сховище
public partial class DrillManager : Node3D
{
	[Export] public PackedScene DrillScene;
	[Export] public float PlaceDistance = 4f;     // м перед машиною
	[Export] public float MaxDropHeight = 30f;    // для дрона: максимум до землі
	[Export] public float MaxSlope = 25f;         // градусів
	[Export] public float CollectRadius = 4f;
	[Export(PropertyHint.Layers3DPhysics)] public uint GroundMask = 2;
	[Export] public float AutosaveInterval = 30f;
	[Export] public float PickupHoldTime = 1f;    // с — скільки тримати X, щоб розібрати бур

	private const string SavePath = "user://save1.json";

	private readonly List<Drill> _drills = new();
	private Node3D _ghost;
	private StandardMaterial3D _ghostOk, _ghostBad;
	private bool _placing, _placeValid;
	private Transform3D _placeXform;
	private ResourceVein _placeVein;
	private float _autosaveTimer;
	private Label _hud;
	private Label _drillHints;
	private Label _notice;
	private CanvasLayer _hudLayer;
	private string _message = "";
	private float _messageTimer;
	private float _pickupHold;
	private Node3D _placingVehicle;
	private Node _settings;
	private Callable _settingsChanged;
	private readonly Dictionary<string, string> _bindingLabels = new();

	public override void _Ready()
	{
		DrillScene ??= GD.Load<PackedScene>("res://Scene/Drill.tscn");
		_settings = GetNodeOrNull<Node>("/root/UserSettings");
		_settingsChanged = Callable.From(ReadBindings);
		_settings?.Connect("changed", _settingsChanged);
		ReadBindings();
		CreateHud();
		CreateGhost();
		CallDeferred(MethodName.Load); // після _Ready жил, щоб їхнє каміння вже існувало
	}

	// ---------- Схема (blueprint) ----------

	private void CreateGhost()
	{
		_ghostOk = MakeGhostMat(new Color(0.2f, 0.7f, 1f, 0.35f));
		_ghostBad = MakeGhostMat(new Color(1f, 0.15f, 0.1f, 0.35f));

		var model = GD.Load<PackedScene>("res://ASS/Drill/Research_Drill/Research_Drill.glb");
		_ghost = model.Instantiate<Node3D>();
		_ghost.Name = "DrillBlueprint";
		_ghost.Visible = false;
		AddChild(_ghost);
		SetGhostMaterial(_ghostOk);
	}

	private static StandardMaterial3D MakeGhostMat(Color c) => new()
	{
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		AlbedoColor = c,
		CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		NoDepthTest = false,
	};

	private void SetGhostMaterial(Material m)
	{
		foreach (var n in _ghost.FindChildren("*", "MeshInstance3D", true, false))
		{
			var mi = (MeshInstance3D)n;
			mi.MaterialOverride = m;
			mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		}
	}

	private Node3D Vehicle => GameData.InSpectatorMode ? null : GameData.ActiveVehicle;
	private bool CanUseInput => Vehicle != null && GameData.CanDrive(Vehicle) && !GetTree().Paused;

	private void UpdateGhost()
	{
		var v = Vehicle;
		if (v == null) { _ghost.Visible = false; _placeValid = false; return; }

		// перед машиною по горизонталі (у машин перед = +Z)
		Vector3 fwd = v.GlobalBasis.Z; fwd.Y = 0;
		fwd = fwd.LengthSquared() < 0.001f ? Vector3.Forward : fwd.Normalized();
		Vector3 from = v.GlobalPosition + fwd * PlaceDistance + Vector3.Up * 3f;
		Vector3 to = from + Vector3.Down * (MaxDropHeight + 3f);

		var q = PhysicsRayQueryParameters3D.Create(from, to, GroundMask);
		if (v is CollisionObject3D co) q.Exclude = new Godot.Collections.Array<Rid> { co.GetRid() };
		var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);

		if (hit.Count == 0)
		{
			_ghost.Visible = true;
			_ghost.GlobalTransform = new Transform3D(Basis.LookingAt(-fwd, Vector3.Up), from + Vector3.Down * 3f);
			SetValid(false, "немає землі під схемою");
			return;
		}

		Vector3 pos = (Vector3)hit["position"];
		Vector3 normal = (Vector3)hit["normal"];
		var collider = hit["collider"].AsGodotObject();

		// влучили в камінь жили або в землю над жилою — притягуємо схему на верх каменя
		_placeVein = collider is StaticBody3D sb && sb.GetParent() is ResourceVein rv ? rv : FindVein(pos);
		Basis basis = Basis.LookingAt(-fwd, Vector3.Up);
		if (_placeVein != null)
		{
			pos = _placeVein.TopCenter - basis * LegsCenterOffset; // центр між ніжками — над центром каменя
			normal = Vector3.Up;
		}

		_placeXform = new Transform3D(basis, pos);
		_ghost.Visible = true;
		_ghost.GlobalTransform = _placeXform;

		float slope = Mathf.RadToDeg(normal.AngleTo(Vector3.Up));
		var missing = GameData.Storage.Missing(DrillCost.Cost);
		if (collider is Drill) SetValid(false, "тут уже стоїть бур");
		else if (_placeVein == null) SetValid(false, "тут немає жили");
		else if (_placeVein.Drill != null) SetValid(false, "на цій жилі вже є бур");
		else if (slope > MaxSlope) SetValid(false, $"занадто крутий схил ({slope:0}°)");
		else if (missing.Count > 0) SetValid(false, "не вистачає: " + FormatItems(missing));
		else SetValid(true, "");
	}

	// Центр між ніжками бура відносно його кореня (з chassis_layout.json моделі)
	private static readonly Vector3 LegsCenterOffset = new(0f, 0f, -0.17f);

	private static string FormatItems(Dictionary<string, int> items)
	{
		var parts = new List<string>();
		foreach (var (n, a) in items) parts.Add($"{n} ×{a}");
		return string.Join(", ", parts);
	}

	// Промінь від поверхні вглиб землі — шукаємо зону жили (Area3D на шарі жил)
	private ResourceVein FindVein(Vector3 groundPos)
	{
		var q = PhysicsRayQueryParameters3D.Create(groundPos + Vector3.Up * 0.5f, groundPos + Vector3.Down * 2f, ResourceVein.VeinLayer);
		q.CollideWithAreas = true;
		q.CollideWithBodies = false;
		q.HitFromInside = true;
		var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
		return hit.Count > 0 ? hit["collider"].AsGodotObject() as ResourceVein : null;
	}

	private string _invalidReason = "";
	private void SetValid(bool ok, string reason)
	{
		if (ok != _placeValid) SetGhostMaterial(ok ? _ghostOk : _ghostBad);
		_placeValid = ok;
		_invalidReason = reason;
	}

	private void PlaceDrill()
	{
		if (!_placeValid) { Notify("Не можна поставити: " + _invalidReason); return; }
		if (!GameData.Storage.RemoveAll(DrillCost.Cost)) { Notify("Не вистачає ресурсів"); return; }
		var d = SpawnDrill(_placeXform, _placeVein.ResourceName, _placeVein.ItemsPerMinute, _placeVein.DrillCapacity);
		LinkVein(d, _placeVein);
		d.AdaptLegs();
		_placing = false;
		_ghost.Visible = false;
		Notify($"Бур встановлено: {d.ResourceName}");
		Save();
	}

	private static void LinkVein(Drill d, ResourceVein v)
	{
		if (v == null) return;
		d.Vein = v;
		v.Drill = d;
	}

	// Жила, на якій стоїть бур (для старих збережень без шляху до жили)
	private ResourceVein VeinAt(Vector3 p)
	{
		foreach (var n in GetTree().Root.FindChildren("*", "Area3D", true, false))
			if (n is ResourceVein v && v.Drill == null && v.GlobalPosition.DistanceTo(p) < v.RockRadius + 1f)
				return v;
		return null;
	}

	private Drill SpawnDrill(Transform3D xf, string resource, float perMinute, int capacity)
	{
		var d = DrillScene.Instantiate<Drill>();
		// налаштування беруться з жили до _Ready
		d.ResourceName = resource;
		d.ItemsPerMinute = perMinute;
		d.Capacity = capacity;
		AddChild(d);
		d.GlobalTransform = xf;
		_drills.Add(d);
		return d;
	}

	// ---------- Збір ----------

	// Найближчий бур до машини. Для дрона відстань по горизонталі — можна працювати з повітря.
	private Drill DrillNearVehicle(float radius)
	{
		var v = Vehicle;
		if (v == null) return null;
		Vector3 p = v.GlobalPosition;
		Drill best = null; float bestD = radius;
		foreach (var d in _drills)
		{
			Vector3 a = d.GlobalPosition;
			float dist = v is CharacterBody3D
				? new Vector2(a.X - p.X, a.Z - p.Z).Length()
				: a.DistanceTo(p);
			if (dist <= bestD) { bestD = dist; best = d; }
		}
		return best;
	}

	private void Collect()
	{
		if (Vehicle == null) return;
		var best = DrillNearVehicle(CollectRadius);
		if (best == null) { Notify("Поруч немає бура"); return; }

		int amount = best.StoredAmount;
		if (amount == 0) { Notify("Бур ще нічого не накопав"); return; }
		best.TakeAllTo(GameData.Storage);
		Notify($"Зібрано: {best.ResourceName} ×{amount}");
		Save();
	}

	// ---------- Розбирання ----------

	private void Pickup(Drill d)
	{
		int mined = d.StoredAmount;
		d.TakeAllTo(GameData.Storage);

		var refund = new Dictionary<string, int>();
		foreach (var (name, amount) in DrillCost.Cost)
		{
			int back = Mathf.FloorToInt(amount * DrillCost.RefundRatio);
			if (back <= 0) continue;
			GameData.Storage.Add(name, back);
			refund[name] = back;
		}

		if (d.Vein != null && IsInstanceValid(d.Vein) && d.Vein.Drill == d) d.Vein.Drill = null;
		_drills.Remove(d);
		d.QueueFree();

		string msg = "Бур розібрано";
		if (mined > 0) msg += $", зібрано {d.ResourceName} ×{mined}";
		if (refund.Count > 0) msg += $", повернуто: {FormatItems(refund)}";
		Notify(msg);
		Save();
	}

	// ---------- Старт і захист від глухого кута ----------

	private void GrantStartResources()
	{
		var start = DrillCost.StartResources;
		foreach (var (name, amount) in start) GameData.Storage.Add(name, amount);
		if (start.Count > 0) Notify("Надано стартовий набір");
	}

	// Немає жодного бура й не вистачає на новий — додаємо те, чого бракує
	private void EnsureCanBuild()
	{
		if (!DrillCost.SoftlockProtection || _drills.Count > 0) return;
		var missing = GameData.Storage.Missing(DrillCost.Cost);
		if (missing.Count == 0) return;
		foreach (var (name, amount) in missing) GameData.Storage.Add(name, amount);
		Notify("Додано аварійний набір на один бур");
		Save();
	}

	// ---------- Ввід і цикл ----------

	public override void _UnhandledInput(InputEvent e)
	{
		if (!CanUseInput || e.IsEcho()) return;
		if (e.IsActionPressed("DrillPlace"))
		{
			EnsureCanBuild();
			_placing = !_placing;
			_placingVehicle = _placing ? Vehicle : null;
			if (!_placing) _ghost.Visible = false;
			GetViewport().SetInputAsHandled();
		}
		else if (_placing && (e.IsActionPressed("DrillConfirm")
			|| e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left))
		{
			PlaceDrill();
			GetViewport().SetInputAsHandled();
		}
		else if (e.IsActionPressed("interact"))
		{
			Collect();
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Process(double delta)
	{
		if (_placing && (!CanUseInput || _placingVehicle != Vehicle))
		{
			_placing = false;
			_placingVehicle = null;
			_ghost.Visible = false;
		}

		if (_placing)
		{
			UpdateGhost();
		}

		// розбирання — треба утримувати X, щоб не розібрати випадково
		var near = _placing || !CanUseInput ? null : DrillNearVehicle(CollectRadius);
		if (near != null && Input.IsActionPressed("DrillPickup"))
		{
			_pickupHold += (float)delta;
			if (_pickupHold >= PickupHoldTime) { _pickupHold = 0; Pickup(near); }
		}
		else _pickupHold = 0;

		_autosaveTimer += (float)delta;
		if (_autosaveTimer >= AutosaveInterval) { _autosaveTimer = 0; Save(); }

		if (_messageTimer > 0) _messageTimer -= (float)delta;
		UpdateHud();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest) Save();
	}

	// ---------- HUD ----------

	private void CreateHud()
	{
		_hudLayer = new CanvasLayer { Layer = 11 };
		Node room = GetParent().GetNodeOrNull<Node>("CommandRoom");
		Node display = this;
		if (room != null && room.HasMethod("get_display_viewport"))
			display = room.Call("get_display_viewport").AsGodotObject() as Node ?? this;
		display.AddChild(_hudLayer);
		var layout = new Control { Name = "ResourcesOverlay", MouseFilter = Control.MouseFilterEnum.Ignore };
		_hudLayer.AddChild(layout);
		layout.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_hud = MakeHudLabel(layout, "ResourceHud", 20, 1);
		_hud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
		_hud.OffsetLeft = 24;
		_hud.OffsetRight = -280; // Reserve the two vehicle identification cutouts.
		_hud.OffsetTop = 54;
		_hud.OffsetBottom = 82;
		_notice = MakeHudLabel(layout, "ResourceNotice", 20, 1);
		_notice.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
		_notice.OffsetLeft = 24;
		_notice.OffsetRight = -280;
		_notice.OffsetTop = 84;
		_notice.OffsetBottom = 112;
		_drillHints = MakeHudLabel(layout, "DrillHints", 21, 2);
		_drillHints.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
		_drillHints.OffsetLeft = 24;
		_drillHints.OffsetRight = -24;
		_drillHints.OffsetTop = -132;
		_drillHints.OffsetBottom = -86;
		_drillHints.VerticalAlignment = VerticalAlignment.Bottom;
	}

	private static Label MakeHudLabel(Control parent, string name, int fontSize, int lines)
	{
		var label = new Label {
			Name = name, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false,
			ClipText = true, MaxLinesVisible = lines, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
		};
		label.AddThemeFontOverride("font", GD.Load<Font>("res://ASS/UI/Fonts/Onest.ttf"));
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_outline_color", Colors.Black);
		label.AddThemeConstantOverride("outline_size", 4);
		parent.AddChild(label);
		return label;
	}

	private void Notify(string msg) { _message = msg; _messageTimer = 3f; }

	private void UpdateHud()
	{
		_hud.Visible = Vehicle != null;
		_drillHints.Visible = _hud.Visible;
		_notice.Visible = _hud.Visible && _messageTimer > 0;
		if (!_hud.Visible) return;
		var all = GameData.Storage.GetAll();
		var inventory = new List<string>();
		foreach (var (name, amount) in all) inventory.Add($"{name}: {amount}");
		_hud.Text = "Сховище · " + (inventory.Count == 0 ? "порожньо" : string.Join("   ·   ", inventory));
		_notice.Text = _message;
		if (_placing)
		{
			string cost = DrillCost.Cost.Count > 0 ? $"Ціна: {FormatItems(new Dictionary<string, int>(DrillCost.Cost))}" : "Схема бура";
			string action = _placeValid
				? $"Жила: {_placeVein.ResourceName}   [ЛКМ/{Binding("DrillConfirm")}] поставити бур"
				: _invalidReason;
			_drillHints.Text = cost + "\n" + action + $"   [{Binding("DrillPlace")}] скасувати";
			return;
		}
		var drill = DrillNearVehicle(CollectRadius);
		if (drill == null)
		{
			_drillHints.Text = $"[{Binding("DrillPlace")}] схема бура";
			return;
		}
		string progress = _pickupHold > 0 ? $"Розбираю… {(int)(100 * _pickupHold / PickupHoldTime)}%" : $"[{Binding("interact")}] забрати ресурси   [{Binding("DrillPickup")} утримуй] розібрати";
		_drillHints.Text = $"Бур · {drill.ResourceName}: {drill.StoredAmount}/{drill.Capacity}" + (drill.CurrentState == Drill.State.Full ? " · повний" : "")
			+ "\n" + progress;
	}

	private string Binding(string action) => _bindingLabels.GetValueOrDefault(action, "Не призначено");
	private void ReadBindings()
	{
		foreach (string action in new[] { "interact", "DrillPickup", "DrillConfirm", "DrillPlace" })
			_bindingLabels[action] = _settings?.Call("binding_text", action).AsString() ?? "Не призначено";
	}

	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(_hudLayer)) _hudLayer.QueueFree();
		if (GodotObject.IsInstanceValid(_settings) && _settings.IsConnected("changed", _settingsChanged))
			_settings.Disconnect("changed", _settingsChanged);
	}

	// ---------- Збереження ----------

	private class DrillSave
	{
		public float[] Transform { get; set; }  // 12 чисел: basis (x,y,z) + origin
		public Dictionary<string, int> Items { get; set; }
		public float Progress { get; set; }
		public string Resource { get; set; }
		public float ItemsPerMinute { get; set; }
		public int Capacity { get; set; }
		public string VeinPath { get; set; }
	}

	// Бури однієї планети + коли їх востаннє зберегли (для офлайн-видобутку саме цієї планети)
	private class PlanetDrills
	{
		public long SavedAtUnix { get; set; }
		public List<DrillSave> Drills { get; set; }
	}

	private class SaveFile
	{
		public long SavedAtUnix { get; set; }          // для Drills (сцени без планети, напр. A.tscn)
		public Dictionary<string, int> Storage { get; set; }
		public List<DrillSave> Drills { get; set; }
		public Dictionary<string, PlanetDrills> Planets { get; set; } // ключ — PlanetKey
	}

	// Ключ планети для збережень бурів. null — сцена без планети (бури в SaveFile.Drills).
	// Планетний режим — лише коли менеджер стоїть на терені (TerrainWorld) і планету вибрано:
	// у шатлі CurrentGen.HasActivePlanet лишається true, але там свої бури.
	private string PlanetKey
	{
		get
		{
			if (!CurrentGen.HasActivePlanet) return null;
			bool onTerrain = false;
			for (Node n = this; n != null; n = n.GetParent())
				if (n is TerrainWorld) { onTerrain = true; break; }
			if (!onTerrain) return null;
			PlanetData p = CurrentGen.ActivePlanet;
			return $"{(p.IsMoon ? "moon" : "planet")}_{p.PlanetSeed}";
		}
	}

	private static SaveFile ReadSave()
	{
		if (!FileAccess.FileExists(SavePath)) return null;
		try
		{
			using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
			return f == null ? null : JsonSerializer.Deserialize<SaveFile>(f.GetAsText());
		}
		catch (Exception) { return null; }
	}

	public void Save()
	{
		long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var drills = new List<DrillSave>();
		var existing = ReadSave();
		string key = PlanetKey;
		var data = new SaveFile
		{
			Storage = GameData.Storage.GetAll(),
			// бури інших місць лишаються як були
			SavedAtUnix = key == null ? now : existing?.SavedAtUnix ?? now,
			Drills = key == null ? drills : existing?.Drills ?? new List<DrillSave>(),
			Planets = existing?.Planets ?? new Dictionary<string, PlanetDrills>(),
		};
		if (key != null) data.Planets[key] = new PlanetDrills { SavedAtUnix = now, Drills = drills };
		foreach (var d in _drills)
		{
			var t = d.GlobalTransform;
			drills.Add(new DrillSave
			{
				Transform = new[] { t.Basis.X.X, t.Basis.X.Y, t.Basis.X.Z, t.Basis.Y.X, t.Basis.Y.Y, t.Basis.Y.Z,
				                    t.Basis.Z.X, t.Basis.Z.Y, t.Basis.Z.Z, t.Origin.X, t.Origin.Y, t.Origin.Z },
				Items = d.Stored.GetAll(),
				Progress = d.Progress,
				Resource = d.ResourceName,
				ItemsPerMinute = d.ItemsPerMinute,
				Capacity = d.Capacity,
				VeinPath = d.Vein != null && IsInstanceValid(d.Vein) ? GetPathTo(d.Vein).ToString() : null,
			});
		}

		using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
		if (f == null) { GD.PushError($"Не вдалося зберегти: {FileAccess.GetOpenError()}"); return; }
		f.StoreString(JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
	}

	private void Load()
	{
		if (!FileAccess.FileExists(SavePath))
		{
			// нова гра
			GrantStartResources();
			Save();
			return;
		}

		SaveFile data;
		try
		{
			using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
			data = JsonSerializer.Deserialize<SaveFile>(f.GetAsText());
		}
		catch (Exception ex)
		{
			GD.PushError($"Збереження пошкоджене, починаю заново: {ex.Message}");
			return;
		}
		if (data == null) return;

		GameData.Storage = new Resources();
		if (data.Storage != null)
			foreach (var (n, a) in data.Storage) GameData.Storage.Add(n, a);

		// Бури цього місця: планети (за ключем) або сцени без планети
		string key = PlanetKey;
		List<DrillSave> drills = data.Drills;
		long savedAt = data.SavedAtUnix;
		if (key != null)
		{
			drills = null;
			if (data.Planets != null && data.Planets.TryGetValue(key, out var planet) && planet != null)
			{
				drills = planet.Drills;
				savedAt = planet.SavedAtUnix;
			}
		}

		// Скільки часу гри не було. Якщо годинник перевели назад — нічого не нараховуємо.
		double away = Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeSeconds() - savedAt);
		int offlineTotal = 0;

		foreach (var ds in drills ?? new List<DrillSave>())
		{
			if (ds.Transform == null || ds.Transform.Length != 12) continue;
			var a = ds.Transform;
			var xf = new Transform3D(new Vector3(a[0], a[1], a[2]), new Vector3(a[3], a[4], a[5]),
			                         new Vector3(a[6], a[7], a[8]), new Vector3(a[9], a[10], a[11]));
			// старі збереження без типу ресурсу — значення за замовчуванням
			var d = SpawnDrill(xf, ds.Resource ?? "Залізна руда",
				ds.ItemsPerMinute > 0 ? ds.ItemsPerMinute : 6f, ds.Capacity > 0 ? ds.Capacity : 50);
			if (ds.Items != null) foreach (var (n, amt) in ds.Items) d.Stored.Add(n, amt);
			d.SetProgress(ds.Progress);
			var vein = ds.VeinPath != null ? GetNodeOrNull<ResourceVein>(ds.VeinPath) : null;
			LinkVein(d, vein != null && vein.Drill == null ? vein : VeinAt(xf.Origin));
			d.AdaptLegs();
			d.SkipDeploy();
			offlineTotal += d.AddOfflineTime(away);
		}

		if (offlineTotal > 0)
			Notify($"Поки тебе не було ({FormatTime(away)}), бури накопали {offlineTotal}");

		EnsureCanBuild(); // старе збереження, де гравець уже застряг
	}

	private static string FormatTime(double s)
	{
		var t = TimeSpan.FromSeconds(s);
		if (t.TotalDays >= 1) return $"{(int)t.TotalDays} д {t.Hours} год";
		if (t.TotalHours >= 1) return $"{(int)t.TotalHours} год {t.Minutes} хв";
		return $"{t.Minutes} хв {t.Seconds} с";
	}
}
