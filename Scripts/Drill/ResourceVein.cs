using Godot;

// Рудна жила. Став її на землю (інстанс Scene/Vein.tscn) і налаштуй в інспекторі ресурс.
// На жилі автоматично ставиться камінь з набору White Cat (за назвою ресурсу) — бур ставиться зверху на нього.
// Для невідомого ресурсу камінь генерується процедурно.
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

		if (!BuildModelRock()) GenerateRock();
	}

	private const string RockDir = "res://ASS/QweQ/White cat камінці/glb/";

	// Модель і зсув UV по спільному атласу (тайл 0.25) — так вибирається кольоровий варіант.
	// Взято камені з пласким верхом, щоб бур стояв рівно.
	private static (string File, Vector2 UvOffset)? RockModelFor(string resource)
	{
		string r = resource.ToLowerInvariant();
		if (r.Contains("титан")) return ("outcrop_shale_sheet.glb", Vector2.Zero);                 // сірий сланець
		if (r.Contains("мід")) return ("outcrop_sandstone_terrace.glb", new Vector2(0.25f, 0f));   // рудо-червоний (bed_iron)
		if (r.Contains("лід") || r.Contains("льод")) return ("ice_tabular_floe.glb", new Vector2(-0.25f, 0f)); // блакитний лід
		return null;
	}

	private bool BuildModelRock()
	{
		if (RockModelFor(ResourceName) is not { } model) return false;
		if (GD.Load<PackedScene>(RockDir + model.File) is not { } scene) return false;

		var root = scene.Instantiate();
		var srcMi = root as MeshInstance3D;
		if (srcMi == null)
			foreach (var n in root.FindChildren("*", "MeshInstance3D", true, false)) { srcMi = (MeshInstance3D)n; break; }
		Mesh mesh = srcMi?.Mesh;
		root.Free();
		if (mesh == null) return false;

		// масштаб: вужчий бік каменя = діаметр площадки
		Aabb box = mesh.GetAabb();
		float scale = 2f * RockRadius / Mathf.Max(0.01f, Mathf.Min(box.Size.X, box.Size.Z));

		int seed = RockSeed != 0 ? RockSeed : (int)GD.Hash(Name.ToString());
		float yaw = new RandomNumberGenerator { Seed = (ulong)(uint)seed }.Randf() * Mathf.Tau;

		var mi = new MeshInstance3D { Name = "RockMesh", Mesh = mesh, Scale = Vector3.One * scale };
		if (model.UvOffset != Vector2.Zero && mesh.SurfaceGetMaterial(0) is BaseMaterial3D baseMat)
		{
			var mat = (BaseMaterial3D)baseMat.Duplicate();
			mat.Uv1Offset = new Vector3(model.UvOffset.X, model.UvOffset.Y, 0f);
			mi.SetSurfaceOverrideMaterial(0, mat);
		}

		// колізія з уже відмасштабованих трикутників (без масштабу на фізичному тілі)
		Vector3[] faces = mesh.GetFaces();
		for (int i = 0; i < faces.Length; i++) faces[i] *= scale;
		var shape = new ConcavePolygonShape3D();
		shape.SetFaces(faces);

		RockHeight = TopAtCenter(faces, box.End.Y * scale);

		Rock = new StaticBody3D { Name = "Rock", CollisionLayer = RockLayer, CollisionMask = 0, Rotation = new Vector3(0f, yaw, 0f) };
		Rock.AddChild(mi);
		Rock.AddChild(new CollisionShape3D { Shape = shape });
		AddChild(Rock);
		return true;
	}

	// Висота поверхні каменя над його pivot (x = z = 0)
	private static float TopAtCenter(Vector3[] faces, float fallback)
	{
		float best = float.MinValue;
		var from = new Vector3(0f, 1000f, 0f);
		for (int i = 0; i + 2 < faces.Length; i += 3)
		{
			Variant hit = Geometry3D.RayIntersectsTriangle(from, Vector3.Down, faces[i], faces[i + 1], faces[i + 2]);
			if (hit.VariantType == Variant.Type.Vector3) best = Mathf.Max(best, hit.AsVector3().Y);
		}
		return best == float.MinValue ? fallback : best;
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
