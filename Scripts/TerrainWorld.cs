using Godot;
using System.Collections.Generic;

public partial class TerrainWorld : Node3D
{
	public enum PlanetType
	{
		Rocky,
		Gas
	}

	[Export] public PlanetType Planet = PlanetType.Rocky;

	// Shared chunk settings.
	[Export] public int Seed = 12345;
	[Export] public int ChunkSize = 16;
	[Export] public int RenderDistance = 2;
	[Export] public float CellSize = 2f;
	[Export] public Node3D Player;
	[Export] public bool DebugGround = true;

	// Rocky planet settings.
	[ExportGroup("Rocky Planet")]
	[Export] public float HeightScale = 20f;
	[Export] public float NoiseFrequency = 0.02f;

	// Gas planet settings. Flat surface, animated material, atmospheric fog.
	[ExportGroup("Gas Planet")]
	[Export] public float GasSurfaceHeight = 0f;
	[Export] public float GasFlowSpeed = 0.4f;
	[Export] public Color GasDarkColor = new(0.10f, 0.16f, 0.32f);
	[Export] public Color GasMidColor = new(0.24f, 0.49f, 0.64f);
	[Export] public Color GasLightColor = new(0.70f, 0.83f, 0.80f);
	[Export] public Color GasFogColor = new(0.37f, 0.55f, 0.65f);
	[Export(PropertyHint.Range, "0.0,0.15,0.001")]
	public float GasFogDensity = 0.035f;
	[Export] public bool GasHasCollision = true;

	// Optional: assign the existing WorldEnvironment of the scene.
	// If omitted, the script configures the current World3D environment.
	[Export] public WorldEnvironment FogEnvironment;

	private FastNoiseLite _noise;
	private Material _material;
	private readonly Dictionary<Vector2I, Node3D> _chunks = new();
	private Vector2I _lastChunk = new(int.MaxValue, int.MaxValue);
	private bool _groundChecked;

	public override void _Ready()
	{
		if (ChunkSize < 1 || CellSize <= 0f || RenderDistance < 0)
		{
			GD.PushError("TerrainWorld: ChunkSize/CellSize must be positive and RenderDistance nonnegative.");
			SetPhysicsProcess(false);
			return;
		}

		_noise = new FastNoiseLite
		{
			Seed = Seed,
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = NoiseFrequency
		};

		switch (Planet)
		{
			case PlanetType.Rocky:
				_material = new StandardMaterial3D
				{
					AlbedoColor = new Color(0.35f, 0.65f, 0.3f),
					Roughness = 1f,
					CullMode = BaseMaterial3D.CullModeEnum.Disabled
				};
				break;

			case PlanetType.Gas:
				_material = CreateGasMaterial();
				ConfigureGasFog();
				break;

			default:
				GD.PushError($"TerrainWorld: Unsupported planet type: {Planet}");
				return;
		}

		UpdateChunks();
	}

	private Material CreateGasMaterial()
	{
		// Put GasSurface.gdshader at res://Shaders/GasSurface.gdshader
		var shader = GD.Load<Shader>("res://Shaders/GasSurface.gdshader");
		if (shader == null)
		{
			GD.PushError("TerrainWorld: Missing res://Shaders/GasSurface.gdshader. Using fallback color.");
			return new StandardMaterial3D
			{
				AlbedoColor = GasMidColor,
				Roughness = 0.4f,
				CullMode = BaseMaterial3D.CullModeEnum.Disabled
			};
		}

		var material = new ShaderMaterial { Shader = shader };
		material.SetShaderParameter("seed_offset", Seed * 0.0137f);
		material.SetShaderParameter("flow_speed", GasFlowSpeed);
		material.SetShaderParameter("dark_color", GasDarkColor);
		material.SetShaderParameter("mid_color", GasMidColor);
		material.SetShaderParameter("light_color", GasLightColor);
		return material;
	}

	private void ConfigureGasFog()
	{
		// Duplicate so a project resource isn't edited in place.
		Godot.Environment source = FogEnvironment != null
			? FogEnvironment.Environment
			: GetWorld3D().Environment;

		var environment = source != null
			? (Godot.Environment)source.Duplicate()
			: new Godot.Environment();

		environment.FogEnabled = true;
		environment.FogLightColor = GasFogColor;
		environment.FogDensity = Mathf.Clamp(GasFogDensity, 0f, 1f);
		environment.FogSkyAffect = 0.85f;

		if (FogEnvironment != null)
			FogEnvironment.Environment = environment;
		else
			GetWorld3D().Environment = environment;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (GetPlayerChunk() != _lastChunk)
			UpdateChunks();

		if (DebugGround && !_groundChecked && Player != null)
		{
			_groundChecked = true;
			TestGroundUnderPlayer();
		}
	}

	private Vector2I GetPlayerChunk()
	{
		Vector3 pos = Player != null
			? ToLocal(Player.GlobalPosition)
			: Vector3.Zero;

		float width = ChunkSize * CellSize;
		return new Vector2I(
			Mathf.FloorToInt(pos.X / width),
			Mathf.FloorToInt(pos.Z / width)
		);
	}

	private void UpdateChunks()
	{
		Vector2I center = GetPlayerChunk();
		_lastChunk = center;
		HashSet<Vector2I> needed = new();

		for (int x = -RenderDistance; x <= RenderDistance; x++)
		{
			for (int z = -RenderDistance; z <= RenderDistance; z++)
			{
				Vector2I coord = center + new Vector2I(x, z);
				needed.Add(coord);

				if (!_chunks.ContainsKey(coord))
					GenerateChunk(coord);
			}
		}

		foreach (Vector2I coord in new List<Vector2I>(_chunks.Keys))
		{
			if (needed.Contains(coord))
				continue;

			_chunks[coord].QueueFree();
			_chunks.Remove(coord);
		}
	}

	private void GenerateChunk(Vector2I coord)
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		for (int z = 0; z < ChunkSize; z++)
		{
			for (int x = 0; x < ChunkSize; x++)
			{
				Vector3 a = GetVertex(coord, x, z);
				Vector3 b = GetVertex(coord, x + 1, z);
				Vector3 c = GetVertex(coord, x, z + 1);
				Vector3 d = GetVertex(coord, x + 1, z + 1);

				// Godot: лицева сторона — вершини за годинниковою стрілкою (якщо дивитись на неї).
				// Зверху це a→b→c і b→d→c, тоді лицем угору — і світло, і колізія працюють правильно.
				AddTriangle(st, a, b, c);
				AddTriangle(st, b, d, c);
			}
		}

		ArrayMesh mesh = st.Commit();
		var chunk = new Node3D
		{
			Name = $"Chunk_{coord.X}_{coord.Y}",
			Position = new Vector3(
				coord.X * ChunkSize * CellSize,
				0,
				coord.Y * ChunkSize * CellSize
			)
		};

		var instance = new MeshInstance3D
		{
			Name = "TerrainMesh",
			Mesh = mesh,
			MaterialOverride = _material
		};
		chunk.AddChild(instance);

		// Preserve collisions for the existing car by default.
		// Turn them off for Gas only if the design calls for falling through clouds.
		if (Planet == PlanetType.Rocky || GasHasCollision)
		{
			var shape = mesh.CreateTrimeshShape();
			shape.BackfaceCollision = true;

			var body = new StaticBody3D
			{
				Name = "TerrainBody",
				CollisionLayer = 1,
				CollisionMask = 1
			};
			body.AddChild(new CollisionShape3D
			{
				Name = "TerrainCollision",
				Shape = shape
			});
			chunk.AddChild(body);
		}

		AddChild(chunk);
		_chunks.Add(coord, chunk);
	}

	private Vector3 GetVertex(Vector2I coord, int x, int z)
	{
		int worldX = coord.X * ChunkSize + x;
		int worldZ = coord.Y * ChunkSize + z;

		float y;
		switch (Planet)
		{
			case PlanetType.Gas:
				y = GasSurfaceHeight; // Exactly flat, no displacement.
				break;

			case PlanetType.Rocky:
			default:
				y = _noise.GetNoise2D(worldX, worldZ) * HeightScale;
				break;
		}

		return new Vector3(x * CellSize, y, z * CellSize);
	}

	private static void AddTriangle(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c)
	{
		// при порядку за годинниковою (як у Godot) нормаль лицевої сторони = (c - a) × (b - a)
		Vector3 normal = (c - a).Cross(b - a).Normalized();
		st.SetNormal(normal);
		st.AddVertex(a);
		st.SetNormal(normal);
		st.AddVertex(b);
		st.SetNormal(normal);
		st.AddVertex(c);
	}

	private void TestGroundUnderPlayer()
	{
		Vector3 p = ToLocal(Player.GlobalPosition);
		float maxHeight = Planet == PlanetType.Gas
			? Mathf.Abs(GasSurfaceHeight)
			: Mathf.Abs(HeightScale);

		Vector3 from = ToGlobal(new Vector3(p.X, maxHeight + 50f, p.Z));
		Vector3 to = ToGlobal(new Vector3(p.X, -maxHeight - 50f, p.Z));

		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.CollisionMask = 1;

		if (Player is CollisionObject3D physicsPlayer)
		{
			query.Exclude = new Godot.Collections.Array<Rid>
			{
				physicsPlayer.GetRid()
			};
		}

		var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (result.Count == 0)
		{
			GD.PrintErr("TERRAIN UNDER PLAYER: NO HIT");
			return;
		}

		Vector3 hit = (Vector3)result["position"];
		GD.Print($"TERRAIN UNDER PLAYER: {result["collider"]}");
		GD.Print($"Player Y={Player.GlobalPosition.Y:F2}, ground Y={hit.Y:F2}");
	}
}
