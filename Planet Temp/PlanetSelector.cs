using Godot;

// Вибір планет у сцені system.
// Наведення мишею — планета підсвічується, всі планети зупиняються на орбітах,
// наведена тільки крутиться навколо осі.
// ЛКМ по планеті — панель з інформацією про неї. ЛКМ у порожнечу, ✕ або Esc — закрити.
public partial class PlanetSelector : Node
{
	[Export] public Color HoverColor = new(0.45f, 0.75f, 1f);
	[Export] public Color SelectColor = new(1f, 0.8f, 0.35f);
	[Export] public float RayLength = 5000f;

	// Світіння по краю планети, накладається поверх її матеріалу (MaterialOverlay)
	private const string HighlightShader = @"
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_back;
uniform vec4 tint : source_color = vec4(1.0);
void fragment() {
	float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.5);
	ALBEDO = tint.rgb * (0.12 + rim * 1.6);
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
	}

	public override void _ExitTree()
	{
		foreach (var p in SystemValues.Planets)
			if (p != null && IsInstanceValid(p)) { p.FreezeOrbit = false; p.FreezeSpin = false; }
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
		_hovered = PickPlanet();
		ApplyState();
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
		if (hit.Count == 0) return null;

		for (Node n = hit["collider"].AsGodotObject() as Node; n != null; n = n.GetParent())
			if (n is PlanetOrbit p) return p;
		return null;
	}

	private void ApplyState()
	{
		if (_selected != null && !IsInstanceValid(_selected)) Deselect();

		// Поки є наведена/вибрана планета — всі стоять на орбітах (інакше наведена виїжджає
		// з-під курсора і все починає мерехтіти), а сама вона крутиться навколо осі.
		PlanetOrbit focus = _hovered ?? _selected;
		foreach (var p in SystemValues.Planets)
		{
			if (p == null || !IsInstanceValid(p)) continue;
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

	private void Select(PlanetOrbit p)
	{
		_selected = p;
		_title.Text = PlanetName(p.Data);
		_info.Text = InfoText(p);
		_panel.Visible = true;
		ApplyState();
	}

	private void Deselect()
	{
		_selected = null;
		_panel.Visible = false;
		ApplyState();
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

	private static string PlanetName(PlanetData d) => $"Планета {d.Order + 1}";

	private static string TypeName(string t) => t switch
	{
		"Rocky" => "Кам'яниста",
		"GasGigant" => "Газовий гігант",
		_ => t,
	};

	private static string InfoText(PlanetOrbit p)
	{
		PlanetData d = p.Data;
		float dist = p.StarNode != null && p.PlanetNode != null
			? p.PlanetNode.GlobalPosition.DistanceTo(p.StarNode.GlobalPosition)
			: d.Distance;

		return
			$"Тип: {TypeName(d.Type)}\n" +
			$"Радіус: {d.Radius:0.00}\n" +
			$"Маса: {d.Mass:0.000}\n" +
			$"Гравітація: {d.Gravity:0.00}\n" +
			$"Температура: {d.Temperature:0}\n" +
			$"\n" +
			$"Відстань до зірки: {dist:0.0}\n" +
			$"Перигелій / афелій: {d.Perihelion:0.0} / {d.Aphelion:0.0}\n" +
			$"Нахил орбіти: {d.Inclination:0.0}°\n" +
			$"Нахил осі: {d.AxialTilt:0.0}°\n" +
			$"Швидкість обертання: {d.rotationspeed:0.00}\n" +
			$"Сплюснутість: {(1f - d.Squish) * 100f:0.0}%\n" +
			$"Сід: {d.PlanetSeed}";
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
			Text = "ЛКМ — вибрати планету   ПКМ — обертати камеру   СКМ — зсунути   Колесо — наблизити",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorTop = 1f, AnchorBottom = 1f,
			OffsetLeft = 16f, OffsetTop = -36f,
			GrowVertical = Control.GrowDirection.Begin,
			Modulate = new Color(1f, 1f, 1f, 0.6f),
		};
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
