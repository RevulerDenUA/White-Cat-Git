using Godot;

// Рудна жила. Став її на землю (інстанс Scene/Vein.tscn) і налаштуй в інспекторі ресурс.
// На жилі автоматично генерується камінь — бур ставиться зверху на нього.
// На одну жилу — один бур.
[GlobalClass]
public partial class ResourceVein : Area3D
{
	public const uint VeinLayer = 1u << 7; // шар колізій 8 — тільки для жил

	[Export] public string ResourceName = "Залізна руда";
	[Export] public float ItemsPerMinute = 6f;   // швидкість бура на цій жилі
	[Export] public int DrillCapacity = 50;      // скільки бур вміщує
	[Export] public Color MarkerColor = new(0.8f, 0.45f, 0.2f);

	[ExportGroup("Камінь")]
	[Export] public float RockRadius = 0.9f;
	[Export] public float RockHeight = 0.35f;    // висота верхньої площадки над землею
	[Export] public float RockRoughness = 0.25f; // наскільки нерівні боки
	[Export] public float TopBumpiness = 0.05f;  // нерівність верху (під неї підлаштовуються ніжки)
	[Export] public int RockSeed = 0;            // 0 — від назви вузла (кожна жила свій камінь)
	[Export(PropertyHint.Layers3DPhysics)] public uint RockLayer = 2; // шар землі, щоб машини й ніжки бура його бачили

	// Бур, що стоїть на цій жилі (null — вільна)
	public Drill Drill { get; set; }
	public StaticBody3D Rock { get; private set; }

	// Точка, куди ставиться бур (центр верху каменя)
	public Vector3 TopCenter => ToGlobal(new Vector3(0, RockHeight, 0));

	public override void _Ready()
	{
		CollisionLayer = VeinLayer;
		CollisionMask = 0;
		Monitoring = false;

		if (GetNodeOrNull<MeshInstance3D>("Marker") is { } marker)
			marker.MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = new Color(MarkerColor, 0.5f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			};

		GenerateRock();
	}

	private void GenerateRock()
	{
		int seed = RockSeed != 0 ? RockSeed : (int)GD.Hash(Name.ToString());
		var noise = new FastNoiseLite { Seed = seed, Frequency = 1.3f, NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin };

		// беремо низькополігональну сферу і деформуємо її шумом
		var src = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 12, Rings = 7 };
		var arrays = src.GetMeshArrays();
		var verts = (Vector3[])arrays[(int)Mesh.ArrayType.Vertex];
		var idx = (int[])arrays[(int)Mesh.ArrayType.Index];

		float sink = RockHeight * 0.6f; // частина каменя під землею
		for (int i = 0; i < verts.Length; i++)
		{
			Vector3 d = verts[i];
			float n = noise.GetNoise3Dv(d * 2f);
			Vector3 v = d * (1f + n * RockRoughness);
			v = new Vector3(v.X * RockRadius, v.Y * (RockHeight + sink) - sink * 0.5f, v.Z * RockRadius);

			// зрізаємо верх у площадку з невеликою нерівністю
			float top = RockHeight + noise.GetNoise2D(v.X * 3f, v.Z * 3f) * TopBumpiness;
			if (v.Y > top) v.Y = top;
			verts[i] = v;
		}

		// розгортаємо індекси → пласке (low-poly) затінення
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		for (int i = 0; i < idx.Length; i++) st.AddVertex(verts[idx[i]]);
		st.GenerateNormals();
		var mesh = st.Commit();

		var rockColor = new Color(0.35f, 0.32f, 0.3f).Lerp(MarkerColor, 0.35f);
		var mi = new MeshInstance3D
		{
			Name = "RockMesh",
			Mesh = mesh,
			MaterialOverride = new StandardMaterial3D { AlbedoColor = rockColor, Roughness = 1f },
		};

		Rock = new StaticBody3D { Name = "Rock", CollisionLayer = RockLayer, CollisionMask = 0 };
		Rock.AddChild(mi);
		Rock.AddChild(new CollisionShape3D { Shape = mesh.CreateTrimeshShape() });
		AddChild(Rock);
	}
}
