using Godot;

// Шатл на поверхні планети: посадковий майданчик, корпус з відкритою кормою, рампа
// й зона стикування всередині. Апарат, що стоїть у зоні, вважається «в шатлі».
//
// Корпус — тимчасова геометрія: модель шатла є лише у .blend (ASS/QweQ/shutle.blend).
// Коли буде .glb, признач ShuttleModel — заглушка корпусу сховається, колізії й зона лишаться.
public partial class ShuttlePad : Node3D
{
	[Export] public PackedScene ShuttleModel;
	[Export] public float PadRadius = 11f;
	[Export] public float HullWidth = 8.8f;    // X
	[Export] public float HullLength = 16f;    // Z, корма (відкрита) — у бік +Z
	[Export] public float HullHeight = 5.2f;
	[Export] public float RampWidth = 7f;
	[Export] public float MaxRampSlopeDeg = 17f;
	[Export(PropertyHint.Layers3DPhysics)] public uint Layers = 3; // і терен (1), і шар бурів/дрона (2)

	private Area3D _dock;
	private Node3D _placeholder;
	private float _rampEndZ;

	private static readonly Color Hull = new(0.42f, 0.46f, 0.5f);
	private static readonly Color Deck = new(0.22f, 0.25f, 0.29f);
	private static readonly Color Accent = new("c6dae9"); // WhiteCatTheme.ACCENT

	// Ставить шатл на терен навколо початку координат і будує геометрію під рельєф
	public void PlaceOn(TerrainWorld terrain)
	{
		float top = float.MinValue, bottom = float.MaxValue;
		for (float x = -PadRadius; x <= PadRadius; x += 1.5f)
			for (float z = -PadRadius; z <= PadRadius; z += 1.5f)
			{
				if (x * x + z * z > PadRadius * PadRadius) continue;
				float h = terrain.GetHeightAt(x, z);
				top = Mathf.Max(top, h);
				bottom = Mathf.Min(bottom, h);
			}
		top += 0.3f;
		GlobalPosition = new Vector3(0f, top, 0f);

		Build(top - bottom + 3f, terrain, top);
	}

	public bool IsDocked(Node3D body) => body is PhysicsBody3D pb && _dock != null && _dock.OverlapsBody(pb);

	// Апарат стартує всередині шатла на палубі, носом до рампи (+Z), і виїжджає сам.
	// Висота — від низу моделі до її початку координат (ровер ~1.42 м, дрон ~1.0 м) з запасом.
	public Transform3D SpawnTransform(string vehicleId)
	{
		float lift = vehicleId == ShuttleState.IngenuityId ? 1.1f : 1.55f;
		return GlobalTransform * new Transform3D(Basis.Identity, new Vector3(0f, lift, -3f));
	}

	private void Build(float skirtDepth, TerrainWorld terrain, float top)
	{
		var body = new StaticBody3D { Name = "PadBody", CollisionLayer = Layers, CollisionMask = 0 };
		AddChild(body);
		_placeholder = new Node3D { Name = "Placeholder" };
		AddChild(_placeholder);

		// майданчик: диск зверху + «спідниця» до найнижчої точки рельєфу
		AddCylinder(body, "Deck", PadRadius, 0.4f, new Vector3(0, -0.2f, 0), Deck);
		AddCylinder(body, "Skirt", PadRadius - 0.3f, skirtDepth, new Vector3(0, -0.4f - skirtDepth / 2f, 0), Deck * 0.8f);

		// корпус з відкритою кормою (+Z)
		float hw = HullWidth / 2f, hl = HullLength / 2f, t = 0.4f;
		AddBox(body, "WallL", new Vector3(t, HullHeight, HullLength), new Vector3(-hw, HullHeight / 2f, 0), Hull, true);
		AddBox(body, "WallR", new Vector3(t, HullHeight, HullLength), new Vector3(hw, HullHeight / 2f, 0), Hull, true);
		AddBox(body, "Roof", new Vector3(HullWidth + t, t, HullLength), new Vector3(0, HullHeight, 0), Hull, true);
		AddBox(body, "Bow", new Vector3(HullWidth + t, HullHeight, t), new Vector3(0, HullHeight / 2f, -hl), Hull, true);

		// рампа від корми вниз до рельєфу. Кінець має лягти на ґрунт по всій ширині:
		// беремо найнижчу точку під кінцем і трохи заглиблюємо, щоб не було сходинки.
		float z0 = PadRadius - 0.5f; // рампа починається з краю майданчика
		float drop = 0f, length = 8f;
		for (int i = 0; i < 4; i++)
		{
			float zEnd = z0 + length, ground = float.MaxValue;
			for (float x = -RampWidth / 2f; x <= RampWidth / 2f + 0.01f; x += RampWidth / 4f)
				ground = Mathf.Min(ground, terrain.GetHeightAt(x, zEnd));
			drop = top - ground + 0.25f;
			length = Mathf.Clamp(Mathf.Abs(drop) / Mathf.Tan(Mathf.DegToRad(MaxRampSlopeDeg)), 5f, 40f);
		}
		float slope = Mathf.Atan2(drop, length);
		float span = Mathf.Sqrt(length * length + drop * drop) + 1f;
		var ramp = AddBox(body, "Ramp", new Vector3(RampWidth, 0.4f, span),
			new Vector3(0, -drop / 2f - 0.2f, z0 + length / 2f), Deck, false);
		ramp.Mesh.Rotation = ramp.Shape.Rotation = new Vector3(slope, 0, 0);
		_rampEndZ = z0 + length;

		// зона стикування (смуга на підлозі показує, куди заїжджати)
		_dock = new Area3D { Name = "DockZone", CollisionLayer = 0, CollisionMask = 0xFFFFFFFF, Monitorable = false };
		_dock.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(HullWidth - 1.2f, HullHeight - 0.6f, HullLength - 2f) } });
		_dock.Position = new Vector3(0, HullHeight / 2f, 0);
		AddChild(_dock);

		var stripe = new MeshInstance3D
		{
			Name = "DockMarking",
			Mesh = new PlaneMesh { Size = new Vector2(HullWidth - 1.6f, HullLength - 2.4f) },
			Position = new Vector3(0, 0.02f, 0),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = new Color(Accent, 0.18f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			},
		};
		AddChild(stripe);

		// маяк над шатлом, щоб знайти його здалеку
		AddChild(new OmniLight3D { Name = "Beacon", Position = new Vector3(0, HullHeight + 1.5f, 0), LightColor = Accent, LightEnergy = 3f, OmniRange = 25f });
		var label = new Label3D
		{
			Name = "Sign",
			Text = "ШАТЛ",
			FontSize = 96,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = new Vector3(0, HullHeight + 2.6f, 0),
			Modulate = Accent,
			OutlineSize = 12,
			NoDepthTest = false,
		};
		AddChild(label);

		if (ShuttleModel != null)
		{
			_placeholder.Visible = false;
			AddChild(ShuttleModel.Instantiate());
		}
	}

	private (MeshInstance3D Mesh, CollisionShape3D Shape) AddBox(StaticBody3D body, string name, Vector3 size, Vector3 pos, Color c, bool hullPart)
	{
		var mesh = new MeshInstance3D { Name = name, Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = Mat(c) };
		(hullPart ? _placeholder : (Node)this).AddChild(mesh);
		var shape = new CollisionShape3D { Name = name + "Shape", Shape = new BoxShape3D { Size = size }, Position = pos };
		body.AddChild(shape);
		return (mesh, shape);
	}

	private void AddCylinder(StaticBody3D body, string name, float radius, float height, Vector3 pos, Color c)
	{
		AddChild(new MeshInstance3D { Name = name, Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height, RadialSegments = 40 }, Position = pos, MaterialOverride = Mat(c) });
		body.AddChild(new CollisionShape3D { Name = name + "Shape", Shape = new CylinderShape3D { Radius = radius, Height = height }, Position = pos });
	}

	private static StandardMaterial3D Mat(Color c) => new() { AlbedoColor = c, Metallic = 0.5f, Roughness = 0.55f };
}
