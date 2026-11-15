using Godot;

// Вибір планет у сцені system.
// Наведення мишею — планета підсвічується, всі планети зупиняються на орбітах,
// наведена тільки крутиться навколо осі.
// ЛКМ по планеті — панель з інформацією про неї. ЛКМ у порожнечу, ✕ або Esc — закрити.
// A / D (дії lefty / righty) — попередня / наступна планета, камера летить до неї.
// Телескоп шатла керує цим вузлом через публічні методи (SelectStep, Selected, MouseEnabled).
public partial class PlanetSelector : Node
{
	[Signal] public delegate void SelectionChangedEventHandler();

	[Export] public Color HoverColor = new(0.45f, 0.75f, 1f);
	[Export] public Color SelectColor = new(1f, 0.8f, 0.35f);
	[Export] public float RayLength = 5000f;
	[Export] public float PickTolerancePx = 12f; // якщо промінь промахнувся — тіло в межах стількох пікселів від курсора
	[Export] public bool MouseEnabled = true;     // телескоп вимикає — там лише клавіатура
	[Export] public bool KeyboardEnabled = true;  // A/D у самій сцені; телескоп викликає SelectStep сам
	[Export] public bool ShowPanel = true;        // у телескопі своя панель
	[Export] public string HelpText = "A / D — перемикати планети   ЛКМ — вибрати   ПКМ — обертати камеру   СКМ — зсунути   Колесо — наблизити";

	// Світіння зірки (емісія 26) закриває планети поруч — лишаємо тільки дрібні рівні
	[ExportGroup("Glow")]
	[Export] public bool TameGlow = true;
	[Export(PropertyHint.Range, "0,2,0.05")] public float GlowIntensity = 0.35f;
	[Export(PropertyHint.Range, "0,4,0.1")] public float GlowHdrThreshold = 1.6f;
	[ExportGroup("")]

	public PlanetOrbit Selected => _selected != null && IsInstanceValid(_selected) ? _selected : null;

	// Світіння по краю планети, накладається поверх її матеріалу (MaterialOverlay)
	private const string HighlightShader = @"
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_back;
uniform vec4 tint : source_color = vec4(1.0);
void fragment() {
	float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.5);
	ALBEDO = tint.rgb * rim * 1.5; // лише край — текстура й грози планети лишаються видні
}";

	private ShaderMaterial _hoverMat, _selectMat;
	private PlanetOrbit _hovered, _selected;

	private Label _tooltip;
	private PanelContainer _panel;
	private Label _title, _info;

	public override void _Ready()
	{
		var shader = new Shader { Code = HighlightShader };
		_hoverMat = MakeMat(shader, HoverColor);
		_selectMat = MakeMat(shader, SelectColor);
		BuildUi();
		if (TameGlow) ReduceGlow();
	}

	// Власна копія оточення сцени (спільний .tres інших сцен не чіпаємо)
	private void ReduceGlow()
	{
		if (GetParent()?.GetNodeOrNull<WorldEnvironment>("WorldEnvironment") is not { Environment: { } env } we) return;
		var copy = (Godot.Environment)env.Duplicate();
		copy.GlowIntensity = GlowIntensity;
		copy.GlowHdrThreshold = GlowHdrThreshold;
		copy.GlowBloom = 0f;
		// великі рівні дають ореол на пів екрана — вимикаємо
		for (int level = 0; level < 7; level++)
			copy.SetGlowLevel(level, level < 2 ? 1f : level == 2 ? 0.4f : 0f);
		we.Environment = copy;
	}

	public override void _ExitTree()
	{
		foreach (var p in AllOrbits())
			{ p.FreezeOrbit = false; p.FreezeSpin = false; }
	}

	// Планети й місяці (місяці не входять у SystemValues.Planets)
	private System.Collections.Generic.IEnumerable<PlanetOrbit> AllOrbits()
	{
		foreach (Node n in GetTree().GetNodesInGroup(PlanetOrbit.GroupName))
			if (n is PlanetOrbit p && IsInstanceValid(p)) yield return p;
	}

	private static ShaderMaterial MakeMat(Shader shader, Color c)
	{
		var m = new ShaderMaterial { Shader = shader };
		m.SetShaderParameter("tint", c);
		return m;
	}

	// ---------- Наведення ----------

	public override void _PhysicsProcess(double delta)
	{
		_hovered = MouseEnabled ? PickPlanet() : null;
		ApplyState();
	}

	// ---------- A / D ----------

	// Планети (без місяців) за порядком від зірки
	public System.Collections.Generic.List<PlanetOrbit> OrderedPlanets()
	{
		var list = new System.Collections.Generic.List<PlanetOrbit>();
		foreach (var p in AllOrbits()) if (!p.Data.IsMoon) list.Add(p);
		list.Sort((a, b) => a.Data.Order.CompareTo(b.Data.Order));
		return list;
	}

	// dir = -1 / +1. Місяць вибраний — крок іде від його планети.
	public void SelectStep(int dir)
	{
		var list = OrderedPlanets();
		if (list.Count == 0) return;
		PlanetOrbit current = Selected;
		if (current != null && current.Data.IsMoon) current = current.ParentOrbit;
		int i = current == null ? (dir > 0 ? -1 : list.Count) : list.IndexOf(current);
		Select(list[((i + dir) % list.Count + list.Count) % list.Count]);
	}

	// Камера летить до вибраної планети й тримає її в центрі
	private void FocusCamera(PlanetOrbit p)
	{
		if (GetViewport().GetCamera3D() is not SystemCamera cam) return;
		if (p?.PlanetNode == null) { cam.ClearFocus(); return; }
		float r = 0.5f * p.PlanetNode.GlobalBasis.Scale.X;
		// камера має стояти за орбітами місяців, інакше вони закривають кадр
		float moons = 0f;
		if (!p.Data.IsMoon)
			foreach (var m in AllOrbits())
				if (m.Data.IsMoon && m.ParentOrbit == p) moons = Mathf.Max(moons, m.Data.Aphelion);
		// за орбітами місяців, але не далі ~12 радіусів — інакше планета з десятком місяців стає крапкою
		cam.FocusOn(p.PlanetNode, Mathf.Clamp(Mathf.Max(r * 7f, moons * 1.6f), 4f, r * 8f + 4f));
	}

	private PlanetOrbit PickPlanet()
	{
		var vp = GetViewport();
		if (vp.GuiGetHoveredControl() != null) return null; // курсор над панеллю
		return PickAt(vp.GetMousePosition());
	}

	// Планета під точкою екрана (null — нічого)
	public PlanetOrbit PickAt(Vector2 screenPos)
	{
		var cam = GetViewport().GetCamera3D();
		if (cam == null) return null;

		Vector3 from = cam.ProjectRayOrigin(screenPos);
		var q = PhysicsRayQueryParameters3D.Create(from, from + cam.ProjectRayNormal(screenPos) * RayLength);
		q.CollideWithAreas = true; // у планет тільки Area3D
		q.CollideWithBodies = false;

		var hit = cam.GetWorld3D().DirectSpaceState.IntersectRay(q);
		if (hit.Count > 0)
			for (Node n = hit["collider"].AsGodotObject() as Node; n != null; n = n.GetParent())
				if (n is PlanetOrbit p) return p;

		return PickNearScreen(cam, screenPos);
	}

	// Дрібні/далекі тіла (місяці) бувають меншими за піксель — беремо найближче до курсора на екрані
	private PlanetOrbit PickNearScreen(Camera3D cam, Vector2 screenPos)
	{
		PlanetOrbit best = null;
		float bestDist = float.MaxValue;
		foreach (var p in AllOrbits())
		{
			if (p.PlanetNode == null) continue;
			Vector3 c = p.PlanetNode.GlobalPosition;
			if (cam.IsPositionBehind(c)) continue;

			Vector2 sc = cam.UnprojectPosition(c);
			// видимий радіус тіла на екрані (меш — сфера радіусом 0.5 у локальних координатах)
			float worldR = 0.5f * p.PlanetNode.GlobalBasis.Scale.X;
			float screenR = sc.DistanceTo(cam.UnprojectPosition(c + cam.GlobalBasis.X * worldR));
			float d = sc.DistanceTo(screenPos);
			if (d <= Mathf.Max(screenR, PickTolerancePx) && d < bestDist) { best = p; bestDist = d; }
		}
		return best;
	}

	private void ApplyState()
	{
		if (_selected != null && !IsInstanceValid(_selected)) Deselect();

		// Поки є наведена/вибрана планета — всі стоять на орбітах (інакше наведена виїжджає
		// з-під курсора і все починає мерехтіти), а сама вона крутиться навколо осі.
		PlanetOrbit focus = _hovered ?? _selected;
		foreach (var p in AllOrbits())
		{
			p.FreezeOrbit = focus != null;
			p.FreezeSpin = focus != null && p != focus;

			if (p.PlanetNode is GeometryInstance3D geo)
			{
				Material want = p == _selected ? _selectMat : p == _hovered ? _hoverMat : null;
				if (geo.MaterialOverlay != want) geo.MaterialOverlay = want;
			}
		}
	}

	// ---------- Клік ----------

	public override void _UnhandledInput(InputEvent e)
	{
		if (KeyboardEnabled && !e.IsEcho() && (e.IsActionPressed("lefty") || e.IsActionPressed("righty")))
		{
			SelectStep(e.IsActionPressed("lefty") ? -1 : 1);
			GetViewport().SetInputAsHandled();
			return;
		}
		if (!MouseEnabled) return;
		if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			if (_hovered != null) Select(_hovered);
			else Deselect();
			GetViewport().SetInputAsHandled();
		}
		else if (e.IsActionPressed("ui_cancel") && _selected != null)
		{
			Deselect();
			GetViewport().SetInputAsHandled();
		}
	}

	public void Select(PlanetOrbit p)
	{
		if (p == null) { Deselect(); return; }
		_selected = p;
		_title.Text = PlanetName(p.Data);
		_info.Text = InfoText(p);
		_panel.Visible = ShowPanel;
		ApplyState();
		FocusCamera(p);
		EmitSignal(SignalName.SelectionChanged);
	}

	public void Deselect()
	{
		bool had = _selected != null;
		_selected = null;
		_panel.Visible = false;
		ApplyState();
		if (GetViewport().GetCamera3D() is SystemCamera cam) cam.ClearFocus();
		if (had) EmitSignal(SignalName.SelectionChanged);
	}

	public override void _Process(double delta)
	{
		// підказка біля курсора
		if (_hovered != null && IsInstanceValid(_hovered))
		{
			_tooltip.Text = $"{PlanetName(_hovered.Data)} · {TypeName(_hovered.Data.Type)}";
			_tooltip.Position = GetViewport().GetMousePosition() + new Vector2(18, 12);
			_tooltip.Visible = true;
		}
		else _tooltip.Visible = false;

		// відстань до зірки змінюється — оновлюємо панель наживо
		if (_selected != null && IsInstanceValid(_selected))
			_info.Text = InfoText(_selected);
	}

	// ---------- Текст ----------

	public static string PlanetName(PlanetData d) => d.IsMoon
		? $"Місяць {d.Order + 1} планети {d.ParentPlanet.Order + 1}"
		: $"Планета {d.Order + 1}";

	public static string TypeName(string t) => t switch
	{
		"Rocky" => "Кам'яниста",
		"GasGigant" => "Газовий гігант",
		_ => t,
	};

	public static string Ua(string s) => s switch
	{
		null or "" => "—",
		"None" => "немає",
		"Vacuum" => "вакуум",
		"Cryo-Gas" => "кріогазова",
		"Hydrogen-Helium" => "воднево-гелієва",
		"Dense-Sulfuric" => "щільна сірчана",
		"Methane-Haze" => "метановий серпанок",
		"Nitrogen-Oxygen" => "азотно-киснева",
		"Compressed-Core" => "стиснене ядро",
		"Iron-Rich" => "багаті на залізо",
		"Cryo-Rock" => "кріопорода",
		"Silicate-Core" => "силікатне ядро",
		"Hydrogen" => "водень",
		"Nitrogen" => "азот",
		"CarbonDioxide" => "вуглекислий газ",
		"Iron" => "залізо",
		"Silicates" => "силікати",
		"WaterIce" => "водяний лід",
		_ => s,
	};

	// "Назва 12.3%" для ненульових складників, від найбільшого
	private static string Composition(params (string Name, float Value)[] parts)
	{
		var list = new System.Collections.Generic.List<(string Name, float Value)>();
		foreach (var p in parts) if (p.Value > 0.05f) list.Add(p);
		list.Sort((a, b) => b.Value.CompareTo(a.Value));
		var sb = new System.Text.StringBuilder();
		foreach (var p in list) sb.Append($"\n   {p.Name} {p.Value:0.#}%");
		return sb.Length > 0 ? sb.ToString() : " —";
	}

	private static string InfoText(PlanetOrbit p)
	{
		PlanetData d = p.Data;
		Node3D center = d.IsMoon ? p.ParentOrbit?.PlanetNode : p.StarNode;
		float dist = center != null && p.PlanetNode != null
			? p.PlanetNode.GlobalPosition.DistanceTo(center.GlobalPosition)
			: d.Distance;

		var t = new System.Text.StringBuilder();
		t.Append(d.IsMoon ? "Тип: місяць\n" : $"Тип: {TypeName(d.Type)}\n");
		if (!d.IsMoon) t.Append($"Місяців: {d.MoonCount}\n");
		t.Append($"Радіус: {d.Radius:0.00}\n");
		t.Append($"Маса: {d.Mass:0.000}\n");
		t.Append($"Гравітація: {d.Gravity:0.00}\n");
		t.Append($"Температура: {d.Temperature:0}\n\n");

		t.Append($"Атмосфера: {Ua(d.AtmoTemplate)}, тиск {d.AtmospherePressure:0.##}\n");
		if (d.AtmospherePressure > 0f)
			t.Append("Гази:" + Composition(
				("водень", d.Hydrogen), ("гелій", d.Helium), ("азот", d.Nitrogen), ("кисень", d.Oxygen),
				("CO₂", d.CarbonDioxide), ("метан", d.Methane), ("аміак", d.Ammonia), ("SO₂", d.SulfurDioxide)) + "\n");
		t.Append($"Надра: {Ua(d.CoreTemplate)}" + Composition(
			("залізо", d.Iron), ("силікати", d.Silicates), ("водяний лід", d.WaterIce), ("вуглець", d.Carbon), ("сірка", d.Sulfur)) + "\n\n");

		t.Append(d.IsMoon ? $"Відстань до планети: {dist:0.0}\n" : $"Відстань до зірки: {dist:0.0}\n");
		t.Append($"Перигелій / афелій: {d.Perihelion:0.0} / {d.Aphelion:0.0}\n");
		t.Append($"Нахил орбіти: {d.Inclination:0.0}°\n");
		t.Append($"Нахил осі: {d.AxialTilt:0.0}°\n");
		t.Append($"Швидкість обертання: {d.rotationspeed:0.00}\n");
		t.Append($"Сплюснутість: {(1f - d.Squish) * 100f:0.0}%\n");
		t.Append($"Сід: {d.PlanetSeed}");
		return t.ToString();
	}

	// ---------- UI ----------

	private void BuildUi()
	{
		var layer = new CanvasLayer { Layer = 10 };
		AddChild(layer);

		_tooltip = new Label { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
		_tooltip.AddThemeColorOverride("font_color", HoverColor);
		_tooltip.AddThemeColorOverride("font_outline_color", Colors.Black);
		_tooltip.AddThemeConstantOverride("outline_size", 6);
		layer.AddChild(_tooltip);

		var help = new Label
		{
			Text = HelpText,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorTop = 1f, AnchorBottom = 1f,
			OffsetLeft = 16f, OffsetTop = -36f,
			GrowVertical = Control.GrowDirection.Begin,
			Modulate = new Color(1f, 1f, 1f, 0.6f),
		};
		help.Visible = HelpText != "";
		help.AddThemeFontSizeOverride("font_size", 14);
		help.AddThemeColorOverride("font_outline_color", Colors.Black);
		help.AddThemeConstantOverride("outline_size", 4);
		layer.AddChild(help);

		_panel = new PanelContainer
		{
			Visible = false,
			AnchorLeft = 1f, AnchorRight = 1f,
			OffsetLeft = -336f, OffsetRight = -16f, OffsetTop = 16f,
			GrowHorizontal = Control.GrowDirection.Begin,
		};
		_panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.04f, 0.06f, 0.1f, 0.88f),
			BorderColor = SelectColor,
			BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
			CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
			ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 12,
		});
		layer.AddChild(_panel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 6);
		_panel.AddChild(vbox);

		var header = new HBoxContainer();
		vbox.AddChild(header);

		_title = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_title.AddThemeColorOverride("font_color", SelectColor);
		_title.AddThemeFontSizeOverride("font_size", 22);
		header.AddChild(_title);

		var close = new Button { Text = "✕", Flat = true };
		close.Pressed += Deselect;
		header.AddChild(close);

		vbox.AddChild(new HSeparator());

		_info = new Label();
		_info.AddThemeFontSizeOverride("font_size", 15);
		vbox.AddChild(_info);
	}
}
