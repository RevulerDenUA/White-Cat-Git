using System.Collections.Generic;
using Godot;

// Телескоп шатла: показує зоряну систему, вибір планети й апарата для висадки.
// Контракт для термінала (command_room.gd):
//   • сцена res://Scene/Telescope.tscn, корінь — цей Control (сам розтягується на батька);
//   • сигнал Closed — гравець вийшов із телескопа назад у термінал;
//   • клавіші приймає в _Input, коли видимий (термінал пересилає ввід у дисплей).
//
// Керування: A / D — планета, W / S — місяці вибраної планети, 1 / 2 — апарат,
// Enter — летіти, Backspace — назад до термінала. Клавіші руху беруться з InputMap.
public partial class TelescopeView : Control
{
	[Signal] public delegate void ClosedEventHandler();

	[Export] public PackedScene SystemScene = GD.Load<PackedScene>("res://Scene/system.tscn");

	// WhiteCatTheme (Scripts/UI/menu_theme.gd)
	private static readonly Color Ink = new("080e17"), PanelBg = new("101923"), White = new("eef0ec"),
		Muted = new("a4b0b9"), Dim = new("738492"), Line = new("2c3a47"), Accent = new("c6dae9"), Warn = new("e8b26a");

	private SubViewport _viewport;
	private PlanetSelector _selector;
	private Label _shuttle, _title, _stats, _message, _hints;
	private readonly Dictionary<string, Label> _vehicleRows = new();
	private string _vehicle = ShuttleState.RoverId;
	private string _message_text = "";

	public PlanetOrbit Target => _selector?.Selected;
	public string Vehicle => _vehicle;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		var themeScript = GD.Load<GDScript>("res://Scripts/UI/menu_theme.gd");
		if (themeScript != null && themeScript.Call("create").AsGodotObject() is Theme theme) Theme = theme;

		var bg = new ColorRect { Color = PanelBg, MouseFilter = MouseFilterEnum.Ignore };
		AddChild(bg);
		bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 0);
		AddChild(row);
		row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		// ---- вид системи ----
		var container = new SubViewportContainer { Stretch = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.75f };
		row.AddChild(container);
		_viewport = new SubViewport { OwnWorld3D = true, HandleInputLocally = false, Msaa3D = Viewport.Msaa.Msaa2X };
		container.AddChild(_viewport);

		Node system = SystemScene.Instantiate();
		_selector = system.GetNodeOrNull<PlanetSelector>("PlanetSelector");
		if (_selector != null)
		{
			_selector.MouseEnabled = false;
			_selector.KeyboardEnabled = false;
			_selector.ShowPanel = false;
			_selector.HelpText = "";
			_selector.SelectionChanged += Refresh;
		}
		_viewport.AddChild(system);

		// ---- панель ----
		var side = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		side.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = PanelBg, BorderColor = Line, BorderWidthLeft = 1,
			ContentMarginLeft = 22, ContentMarginRight = 22, ContentMarginTop = 16, ContentMarginBottom = 14 });
		row.AddChild(side);
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 6);
		side.AddChild(col);

		var head = new HBoxContainer();
		col.AddChild(head);
		var name = MakeLabel("Телескоп", 24, White);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		head.AddChild(name);
		_shuttle = MakeLabel("", 16, Dim);
		head.AddChild(_shuttle);
		col.AddChild(new HSeparator());

		_title = MakeLabel("", 22, Accent);
		col.AddChild(_title);
		_stats = MakeLabel("", 16, Muted);
		col.AddChild(_stats);

		col.AddChild(new HSeparator());
		col.AddChild(MakeLabel("Висадка", 16, Dim));
		foreach (string id in ShuttleState.VehicleIds)
		{
			var l = MakeLabel("", 18, White);
			_vehicleRows[id] = l;
			col.AddChild(l);
		}

		_message = MakeLabel("", 16, Warn);
		_message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_message.SizeFlagsVertical = SizeFlags.ExpandFill;
		col.AddChild(_message);

		_hints = MakeLabel("", 15, Dim);
		_hints.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		col.AddChild(_hints);

		CallDeferred(MethodName.SelectInitial);
	}

	private void SelectInitial()
	{
		if (_selector == null) { Refresh(); return; }
		var planets = _selector.OrderedPlanets();
		PlanetOrbit start = null;
		if (ShuttleState.HasPlanet)
			foreach (Node n in GetTree().GetNodesInGroup(PlanetOrbit.GroupName))
				if (n is PlanetOrbit p && ShuttleState.IsCurrent(p.Data)) start = p;
		start ??= planets.Count > 0 ? planets[0] : null;
		_selector.Select(start);
		Refresh();
	}

	private static Label MakeLabel(string text, int size, Color color)
	{
		var l = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
		l.AddThemeFontSizeOverride("font_size", size);
		l.AddThemeColorOverride("font_color", color);
		return l;
	}

	// ---------- Ввід ----------

	public override void _Input(InputEvent e)
	{
		if (!IsVisibleInTree() || e.IsEcho() || !e.IsPressed()) return;

		bool handled = true;
		if (e.IsActionPressed("lefty")) Step(-1);
		else if (e.IsActionPressed("righty")) Step(1);
		else if (e.IsActionPressed("forwardy")) StepMoon(1);
		else if (e.IsActionPressed("backwardy")) StepMoon(-1);
		else if (e is InputEventKey key)
		{
			switch (key.Keycode)
			{
				case Key.Key1: case Key.Kp1: ChooseVehicle(ShuttleState.RoverId); break;
				case Key.Key2: case Key.Kp2: ChooseVehicle(ShuttleState.IngenuityId); break;
				case Key.Enter: case Key.KpEnter: TryLaunch(); break;
				case Key.Backspace: EmitSignal(SignalName.Closed); break;
				default: handled = false; break;
			}
		}
		else handled = false;

		if (handled) GetViewport().SetInputAsHandled();
	}

	public void Step(int dir)
	{
		_message_text = "";
		_selector?.SelectStep(dir);
	}

	// Місяці вибраної планети: W — наступний, S — попередній (до самої планети)
	public void StepMoon(int dir)
	{
		PlanetOrbit current = Target;
		if (current == null || _selector == null) return;
		PlanetOrbit planet = current.Data.IsMoon ? current.ParentOrbit : current;
		var chain = new List<PlanetOrbit> { planet };
		var moons = new List<PlanetOrbit>();
		foreach (Node n in GetTree().GetNodesInGroup(PlanetOrbit.GroupName))
			if (n is PlanetOrbit p && p.Data.IsMoon && p.ParentOrbit == planet) moons.Add(p);
		moons.Sort((a, b) => a.Data.Order.CompareTo(b.Data.Order));
		chain.AddRange(moons);
		if (chain.Count == 1) { _message_text = "У цієї планети немає місяців."; Refresh(); return; }
		int i = Mathf.Max(chain.IndexOf(current), 0);
		_message_text = "";
		_selector.Select(chain[((i + dir) % chain.Count + chain.Count) % chain.Count]);
	}

	public void ChooseVehicle(string id)
	{
		_vehicle = id;
		_message_text = "";
		Refresh();
	}

	public string TryLaunch()
	{
		PlanetOrbit target = Target;
		if (target == null) { _message_text = "Спершу виберіть планету."; Refresh(); return _message_text; }
		string scene = GetTree().CurrentScene?.SceneFilePath ?? "";
		string error = ShuttleState.Launch(GetTree(), target.Data, _vehicle, scene);
		_message_text = error;
		Refresh();
		return error;
	}

	// ---------- Панель ----------

	private void Refresh()
	{
		if (_title == null) return;
		_shuttle.Text = ShuttleState.HasPlanet ? $"шатл: {PlanetSelector.PlanetName(ShuttleState.Planet)}" : "шатл: на орбіті";

		PlanetOrbit t = Target;
		if (t == null)
		{
			_title.Text = "Ціль не вибрана";
			_stats.Text = "";
		}
		else
		{
			PlanetData d = t.Data;
			_title.Text = PlanetSelector.PlanetName(d) + (ShuttleState.IsCurrent(d) ? " · тут шатл" : "");
			string kind = d.IsMoon ? "місяць" : PlanetSelector.TypeName(d.Type).ToLowerInvariant();
			string moons = d.IsMoon ? "" : $" · місяців {d.MoonCount}";
			_stats.Text =
				$"{kind}{moons}\n" +
				$"радіус {d.Radius:0.00} · гравітація {d.Gravity:0.0}\n" +
				$"температура {d.Temperature:0} · тиск {d.AtmospherePressure:0.##}\n" +
				$"надра: {d.CoreTemplate ?? "—"}";
		}

		foreach (string id in ShuttleState.VehicleIds)
		{
			string key = id == ShuttleState.RoverId ? "1" : "2";
			string state = ShuttleState.IsOutside(id) ? "на поверхні" : "у шатлі";
			string reason = t != null ? ShuttleState.CanLaunch(t.Data, id) : "";
			bool allowed = reason == "";
			bool chosen = id == _vehicle;
			var row = _vehicleRows[id];
			row.Text = $"{(chosen ? "›" : " ")} {key}  {ShuttleState.VehicleName(id)} — {state}{(allowed ? "" : " · недоступно")}";
			row.AddThemeColorOverride("font_color", chosen ? (allowed ? Accent : Warn) : (allowed ? White : Dim));
		}

		string chosenReason = t != null ? ShuttleState.CanLaunch(t.Data, _vehicle) : "";
		_message.Text = _message_text != "" ? _message_text : chosenReason;

		string Bind(string action, string fallback)
		{
			var settings = GetNodeOrNull<Node>("/root/UserSettings");
			return settings != null ? settings.Call("binding_text", action).AsString() : fallback;
		}
		_hints.Text = $"{Bind("lefty", "A")} / {Bind("righty", "D")} — планета   {Bind("forwardy", "W")} / {Bind("backwardy", "S")} — місяці\n" +
			"1 / 2 — апарат   Enter — летіти   Backspace — термінал";
	}
}
