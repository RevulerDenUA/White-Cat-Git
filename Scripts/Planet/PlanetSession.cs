using System.Collections.Generic;
using Godot;

// Висадка на планету: ставить шатл на рельєф, розставляє апарати й одразу передає
// керування вибраному в телескопі. Esc — назад у шатл (командну кімнату).
// Апарат у зоні стикування на момент виходу вважається «в шатлі», інакше лишається
// на поверхні там, де стоїть, і з'явиться там же при наступній висадці на цю планету.
public partial class PlanetSession : Node
{
	[Export] public CameraControl Controller;
	[Export] public TerrainWorld Terrain;
	[Export] public ShuttlePad Pad;
	[Export] public RemoteVehicle Rover;
	[Export] public RemoteVehicle Ingenuity;
	[Export] public OreGenerator Ores;

	private readonly Dictionary<string, RemoteVehicle> _vehicles = new();
	private string _active = "";
	private string _status = "";
	private Label _title, _facts, _live, _status_line, _controls, _hint;
	private readonly TextureRect[] _portraits = new TextureRect[2];

	// Для тестів і іншого UI
	public string CardTitle => _title?.Text ?? "";
	public string CardFacts => _facts?.Text ?? "";
	public bool PhotoVisible => _portraits[0] != null && _portraits[0].Visible;

	// WhiteCatTheme (Scripts/UI/menu_theme.gd)
	private static readonly Color Ink = new("080e17"), White = new("eef0ec"), Muted = new("a4b0b9"),
		Dim = new("738492"), Line = new("2c3a47"), Accent = new("c6dae9");
	private bool _leaving;

	public override void _Ready()
	{
		if (Rover != null) _vehicles[ShuttleState.RoverId] = Rover;
		if (Ingenuity != null) _vehicles[ShuttleState.IngenuityId] = Ingenuity;
		if (Controller != null) Controller.ExitRequested += ReturnToShuttle;
		BuildHud();
		// Терен будує шматки в _Ready; апарати ставимо, коли все вже в дереві
		CallDeferred(MethodName.Setup);
	}

	private void Setup()
	{
		_active = ShuttleState.PendingVehicle;
		if (string.IsNullOrEmpty(_active) || !_vehicles.ContainsKey(_active))
			_active = ShuttleState.RoverId; // сцену запустили напряму з редактора

		Pad?.PlaceOn(Terrain);

		foreach (var (id, adapter) in _vehicles)
		{
			if (!RemoteVehicle.IsLive(adapter) || !RemoteVehicle.IsLive(adapter.Vehicle)) continue;
			Node3D body = adapter.Vehicle;

			if (ShuttleState.TryGetOutside(id, out Transform3D saved))
			{
				Place(body, saved.Translated(Vector3.Up * 0.3f)); // лишився на поверхні
			}
			else if (id == _active)
			{
				Transform3D spawn = Pad.SpawnTransform(id); // всередині шатла
				Place(body, spawn);
				ShuttleState.SetOutside(id, spawn);
			}
			else
			{
				body.QueueFree(); // цей апарат у шатлі
			}
		}

		if (_vehicles.TryGetValue(_active, out RemoteVehicle chosen) && RemoteVehicle.IsLive(chosen.Vehicle))
		{
			Terrain.Player = chosen.Vehicle; // шматки рельєфу вантажаться навколо керованого апарата
			string error = Controller?.connect_target(_active) ?? "Немає контролера керування.";
			if (error != "") _status = error;
		}
		else _status = "Апарата немає в сцені.";

		FillPlanetCard();
	}

	private static void Place(Node3D body, Transform3D xf)
	{
		body.GlobalTransform = xf;
		switch (body)
		{
			case RigidBody3D rb: rb.LinearVelocity = Vector3.Zero; rb.AngularVelocity = Vector3.Zero; break;
			case CharacterBody3D cb: cb.Velocity = Vector3.Zero; break;
		}
	}

	private void ReturnToShuttle()
	{
		if (_leaving) return;
		_leaving = true;
		foreach (var (id, adapter) in _vehicles)
		{
			if (!RemoteVehicle.IsLive(adapter) || !RemoteVehicle.IsLive(adapter.Vehicle)) continue;
			if (Pad != null && Pad.IsDocked(adapter.Vehicle)) ShuttleState.SetInside(id);
			else ShuttleState.SetOutside(id, adapter.Vehicle.GlobalTransform);
		}
		// після обробки Esc: зміна сцени одразу прибирає поточну з дерева
		GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, ShuttleState.ReturnScene);
	}

	// ---------- HUD ----------

	public override void _Process(double delta)
	{
		if (_title == null) return;
		_vehicles.TryGetValue(_active, out RemoteVehicle a);
		Node3D body = a != null && RemoteVehicle.IsLive(a.Vehicle) ? a.Vehicle : null;
		bool docked = body != null && Pad != null && Pad.IsDocked(body);

		// жива частина картки: висота, швидкість, шатл
		if (body != null)
		{
			float ground = Terrain != null ? Terrain.GetHeightAt(body.GlobalPosition.X, body.GlobalPosition.Z) : 0f;
			float speed = body switch { RigidBody3D rb => rb.LinearVelocity.Length(), CharacterBody3D cb => cb.Velocity.Length(), _ => 0f };
			Vector3 toPad = Pad != null ? body.GlobalPosition - Pad.GlobalPosition : Vector3.Zero;
			toPad.Y = 0f;
			_live.Text = $"висота над ґрунтом {Mathf.Max(body.GlobalPosition.Y - ground, 0f):0} м · швидкість {speed:0} м/с\n" +
				(docked ? "у шатлі" : $"до шатла {toPad.Length():0} м");
		}

		var snap = Controller?.snapshot();
		string camera = snap != null ? snap["camera_name"].AsString() : "";
		_status_line.Text = ShuttleState.VehicleName(_active) + (camera != "" ? $" / {camera}" : "");
		_controls.Text = ControlsText(a);
		_hint.Text = _status != "" ? _status
			: docked ? "Esc — завершити висадку"
			: "Esc — до шатла (апарат лишиться тут)";

		var images = a?.IdentificationImages;
		for (int i = 0; i < _portraits.Length; i++)
		{
			_portraits[i].Texture = images != null && i < images.Count ? images[i] : null;
			_portraits[i].Visible = _portraits[i].Texture != null;
		}
	}

	private string ControlsText(RemoteVehicle a)
	{
		var settings = GetNodeOrNull<Node>("/root/UserSettings");
		string B(string action, string fallback) => settings != null && InputMap.HasAction(action)
			? settings.Call("binding_text", action).AsString() : fallback;
		string text = $"{B("forwardy", "W")} / {B("backwardy", "S")} — рух   {B("lefty", "A")} / {B("righty", "D")} — поворот";
		if (a != null && a.SupportsVerticalControl)
			text += $"   {B("spacy", "Space")} / {B("flight_descend", "Shift")} — висота";
		return text + $"\n{B("CameraChange", "C")} — камера   {B("DrillPlace", "B")} — бур";
	}

	// Незмінні дані планети — один раз після висадки
	private void FillPlanetCard()
	{
		if (!CurrentGen.HasActivePlanet)
		{
			_title.Text = "Тестовий рельєф";
			_facts.Text = "планету не вибрано в телескопі";
			return;
		}
		PlanetData d = CurrentGen.ActivePlanet;
		string kind = d.IsMoon ? "місяць" : PlanetSelector.TypeName(d.Type).ToLowerInvariant();
		_title.Text = $"{PlanetSelector.PlanetName(d)} · {kind}";

		var facts = new System.Text.StringBuilder();
		facts.Append($"гравітація {d.Gravity:0.0} · температура {d.Temperature:0}\n");
		facts.Append($"атмосфера: {PlanetSelector.Ua(d.AtmoTemplate)}" + (d.AtmospherePressure > 0f ? $", тиск {d.AtmospherePressure:0.##}" : "") + "\n");
		facts.Append($"надра: {PlanetSelector.Ua(d.CoreTemplate)}\n");

		// жили, які реально згенерував OreGenerator
		var counts = new SortedDictionary<string, int>();
		if (Ores != null)
			foreach (ResourceVein v in Ores.Veins)
				if (IsInstanceValid(v)) counts[v.ResourceName] = counts.GetValueOrDefault(v.ResourceName) + 1;
		if (counts.Count == 0)
			facts.Append(ShuttleState.IsGas(d) ? "жил немає — немає твердої поверхні" : "жил поблизу немає");
		else
		{
			var parts = new List<string>();
			foreach (var (name, n) in counts) parts.Add($"{OreShort(name)} {n}");
			facts.Append("жили поруч: " + string.Join(" · ", parts));
		}
		_facts.Text = facts.ToString();
	}

	private static string OreShort(string name) => name switch
	{
		"Титанова руда" => "титан",
		"Мідна руда" => "мідь",
		"Лід" => "лід",
		_ => name.ToLowerInvariant(),
	};

	private void BuildHud()
	{
		var layer = new CanvasLayer { Layer = 5 };
		AddChild(layer);
		var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		var themeScript = GD.Load<GDScript>("res://Scripts/UI/menu_theme.gd");
		if (themeScript != null && themeScript.Call("create").AsGodotObject() is Theme theme) root.Theme = theme;
		layer.AddChild(root);
		root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		// праворуч угорі — картка планети
		var card = new PanelContainer
		{
			AnchorLeft = 1f, AnchorRight = 1f, OffsetLeft = -380f, OffsetRight = -20f, OffsetTop = 20f,
			GrowHorizontal = Control.GrowDirection.Begin, MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(Ink, 0.72f), BorderColor = Line,
			BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
			ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 12, ContentMarginBottom = 12,
		});
		root.AddChild(card);
		var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		col.AddThemeConstantOverride("separation", 6);
		card.AddChild(col);
		_title = HudLabel(20, Accent);
		_facts = HudLabel(15, Muted);
		_live = HudLabel(15, White);
		col.AddChild(_title);
		col.AddChild(_facts);
		col.AddChild(new HSeparator());
		col.AddChild(_live);

		// ліворуч — фото апарата (під рядком сховища бурів)
		var photos = new VBoxContainer { OffsetLeft = 20f, OffsetTop = 92f, MouseFilter = Control.MouseFilterEnum.Ignore };
		photos.AddThemeConstantOverride("separation", 8);
		root.AddChild(photos);
		for (int i = 0; i < _portraits.Length; i++)
		{
			_portraits[i] = new TextureRect
			{
				CustomMinimumSize = new Vector2(220, 140),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			photos.AddChild(_portraits[i]);
		}

		// праворуч унизу — апарат, керування, вихід
		var box = new VBoxContainer
		{
			AnchorLeft = 1f, AnchorRight = 1f, AnchorTop = 1f, AnchorBottom = 1f,
			OffsetLeft = -620f, OffsetRight = -24f, OffsetTop = -110f, OffsetBottom = -20f,
			GrowHorizontal = Control.GrowDirection.Begin, GrowVertical = Control.GrowDirection.Begin,
			Alignment = BoxContainer.AlignmentMode.End, MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		root.AddChild(box);
		_status_line = HudLabel(18, White);
		_controls = HudLabel(14, Muted);
		_hint = HudLabel(14, Accent);
		foreach (var l in new[] { _status_line, _controls, _hint })
		{
			l.HorizontalAlignment = HorizontalAlignment.Right;
			box.AddChild(l);
		}
	}

	private static Label HudLabel(int size, Color color)
	{
		var l = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
		l.AddThemeFontSizeOverride("font_size", size);
		l.AddThemeColorOverride("font_color", color);
		l.AddThemeColorOverride("font_outline_color", Ink);
		l.AddThemeConstantOverride("outline_size", 4);
		return l;
	}

	public override void _ExitTree()
	{
		if (Controller != null && IsInstanceValid(Controller)) Controller.ExitRequested -= ReturnToShuttle;
	}
}
