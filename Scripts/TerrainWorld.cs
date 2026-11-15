
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

	// World settings
	[ExportGroup("World")]
	[Export] public int Seed = 12345;
	[Export] public int ChunkSize = 16;
	[Export] public int RenderDistance = 2;
	[Export] public float CellSize = 2f;
	[Export] public Node3D Player;
	[Export] public bool DebugGround = true;

	// Rocky settings
	[ExportGroup("Rocky Planet")]
	[Export] public float HeightScale = 20f;
	[Export] public float NoiseFrequency = 0.02f;

	// Gas settings
	[ExportGroup("Gas Planet")]
	[Export] public float GasSurfaceHeight = 0f;
	[Export] public float GasFlowSpeed = 0.4f;

	[Export] public Color GasDarkColor =
		new(0.10f, 0.16f, 0.32f);

	[Export] public Color GasMidColor =
		new(0.24f, 0.49f, 0.64f);

	[Export] public Color GasLightColor =
		new(0.70f, 0.83f, 0.80f);

	[Export] public Color GasFogColor =
		new(0.37f, 0.55f, 0.65f);

	[Export(PropertyHint.Range, "0,0.15,0.001")]
	public float GasFogDensity = 0.035f;

	[Export] public bool GasHasCollision = true;

	// Rocky sky settings
	[ExportGroup("Rocky Sky")]
	[Export] public Color RockySkyDark =
		new(0.13f, 0.17f, 0.28f);

	[Export] public Color RockySkyMid =
		new(0.35f, 0.49f, 0.70f);

	[Export] public Color RockySkyFog =
		new(0.59f, 0.69f, 0.76f);

	[Export] public Color RockySkyLight =
		new(0.76f, 0.85f, 0.95f);

	[Export(PropertyHint.Range, "0,0.1,0.001")]
	public float RockyClearFogDensity = 0.015f;

	// Atmosphere
	[ExportGroup("Atmosphere")]
	[Export] public WorldEnvironment FogEnvironment;

	private FastNoiseLite _noise;
	private Material _material;

	private readonly Dictionary<Vector2I, Node3D> _chunks = new();

	private Vector2I _lastChunk =
		new(int.MaxValue, int.MaxValue);

	private bool _groundChecked = false;

	public override void _Ready()
	{
		if (ChunkSize < 1 ||
			CellSize <= 0f ||
			RenderDistance < 0)
		{
			GD.PushError(
                "TerrainWorld: Invalid chunk settings."
			);

			SetPhysicsProcess(false);
			return;
		}

		// Load generated planet data before
		// initializing noise and materials.
		ApplyActivePlanet();

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
					AlbedoColor =
						new Color(0.35f, 0.65f, 0.3f),

					Roughness = 1f,

					CullMode =
						BaseMaterial3D.CullModeEnum.Disabled
				};
				break;

			case PlanetType.Gas:
				_material = CreateGasMaterial();
				break;

			default:
				GD.PushError(
					$"Unsupported planet type: {Planet}"
				);

				SetPhysicsProcess(false);
				return;
		}

		ConfigurePlanetAtmosphere();
		UpdateChunks();
	}

	// ==================================================
	// PLANET DATA INTEGRATION
	// ==================================================

	private void ApplyActivePlanet()
	{
		// No planet selected: use Inspector settings.
		if (!CurrentGen.HasActivePlanet)
		{
			GD.Print(
				"[TerrainWorld] No active planet. " +
                "Using Inspector settings."
			);

			return;
		}

		PlanetData p = CurrentGen.ActivePlanet;

		// Select planet type from colleague's data.
		switch (p.Type?.Trim().ToLowerInvariant())
		{
			case "gas":
				Planet = PlanetType.Gas;
				break;

			case "rocky":
				Planet = PlanetType.Rocky;
				break;

			default:
				GD.PushWarning(
					$"[TerrainWorld] Unknown planet type: " +
					$"{p.Type ?? "null"}. " +
                    "Using Inspector planet type."
				);
				break;
		}

		// NoiseSeed controls procedural terrain.
		Seed = p.NoiseSeed;

		// Check whether sky colors were generated.
		bool hasSkyColors =
			p.SkyDark.A > 0f &&
			p.SkyMid.A > 0f &&
			p.SkyLight.A > 0f &&
			p.SkyFog.A > 0f;

		if (hasSkyColors)
		{
			// Rocky atmosphere
			RockySkyDark = p.SkyDark;
			RockySkyMid = p.SkyMid;
			RockySkyLight = p.SkyLight;
			RockySkyFog = p.SkyFog;

			// Gas atmosphere and surface colors
			GasDarkColor = p.SkyDark;
			GasMidColor = p.SkyMid;
			GasLightColor = p.SkyLight;
			GasFogColor = p.SkyFog;
		}
		else
		{
			GD.PushWarning(
				"[TerrainWorld] Sky colors are missing. " +
				"Using Inspector colors. Check " +
                "SystemValues.GenerateSkyColors()."
			);
		}

		GD.Print(
			$"[TerrainWorld] Planet loaded: {p.Type}"
		);

		GD.Print(
			$"[TerrainWorld] NoiseSeed: {Seed}"
		);

		GD.Print(
			$"[TerrainWorld] Mass: {p.Mass}, " +
			$"Gravity: {p.Gravity}"
		);
	}

	// ==================================================
	// GAS MATERIAL
	// ==================================================

	private Material CreateGasMaterial()
	{
		var shader = GD.Load<Shader>(
            "res://Shaders/GasSurface.gdshader"
		);

		if (shader == null)
		{
			GD.PushError(
                "Missing GasSurface.gdshader!"
			);

			return new StandardMaterial3D
			{
				AlbedoColor = GasMidColor,
				Roughness = 0.4f,

				CullMode =
					BaseMaterial3D.CullModeEnum.Disabled
			};
		}

		var material = new ShaderMaterial
		{
			Shader = shader
		};

		material.SetShaderParameter(
			"seed_offset",
			Seed * 0.0137f
		);

		material.SetShaderParameter(
			"flow_speed",
			GasFlowSpeed
		);

		material.SetShaderParameter(
			"dark_color",
			GasDarkColor
		);

		material.SetShaderParameter(
			"mid_color",
			GasMidColor
		);

		material.SetShaderParameter(
			"light_color",
			GasLightColor
		);

		return material;
	}

	// ==================================================
	// SKYBOX AND FOG
	// ==================================================

	private void ConfigurePlanetAtmosphere()
	{
		Godot.Environment source =
			FogEnvironment != null
				? FogEnvironment.Environment
				: GetWorld3D().Environment;

		Godot.Environment env = source != null
			? (Godot.Environment)source.Duplicate()
			: new Godot.Environment();

		Color dark;
		Color mid;
		Color haze;
		Color light;

		float density;

		switch (Planet)
		{
			case PlanetType.Gas:
				dark = GasDarkColor;
				mid = GasMidColor;
				haze = GasFogColor;
				light = GasLightColor;
				density = GasFogDensity;
				break;

			case PlanetType.Rocky:
			default:
				dark = RockySkyDark;
				mid = RockySkyMid;
				haze = RockySkyFog;
				light = RockySkyLight;
				density = RockyClearFogDensity;
				break;
		}

		var shader = GD.Load<Shader>(
            "res://Shaders/PlanetSkyGradient.gdshader"
		);

		if (shader != null)
		{
			var skyMaterial = new ShaderMaterial
			{
				Shader = shader
			};

			skyMaterial.SetShaderParameter(
				"sky_dark", dark
			);

			skyMaterial.SetShaderParameter(
				"sky_mid", mid
			);

			skyMaterial.SetShaderParameter(
				"sky_fog", haze
			);

			skyMaterial.SetShaderParameter(
				"sky_light", light
			);

			env.Sky = new Sky
			{
				SkyMaterial = skyMaterial
			};

			env.BackgroundMode =
				Godot.Environment.BGMode.Sky;
		}
		else
		{
			GD.PushError(
                "Missing PlanetSkyGradient.gdshader!"
			);
		}

		// Fog
		env.FogEnabled = true;
		env.FogLightColor = haze;
		env.FogDensity =
			Mathf.Clamp(density, 0f, 1f);

		env.FogSkyAffect = 0.85f;

		if (FogEnvironment != null)
		{
			FogEnvironment.Environment = env;
		}
		else
		{
			GetWorld3D().Environment = env;
		}
	}

	// ==================================================
	// CHUNK UPDATE
	// ==================================================

	public override void _PhysicsProcess(double delta)
	{
		if (GetPlayerChunk() != _lastChunk)
		{
			UpdateChunks();
		}

		if (DebugGround &&
			!_groundChecked &&
			Player != null)
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

		for (int x = -RenderDistance;
			 x <= RenderDistance; x++)
		{
			for (int z = -RenderDistance;
				 z <= RenderDistance; z++)
			{
				Vector2I coord =
					center + new Vector2I(x, z);

				needed.Add(coord);

				if (!_chunks.ContainsKey(coord))
				{
					GenerateChunk(coord);
				}
			}
		}

		foreach (
			Vector2I coord in
			new List<Vector2I>(_chunks.Keys))
		{
			if (needed.Contains(coord))
				continue;

			_chunks[coord].QueueFree();
			_chunks.Remove(coord);
		}
	}

	// ==================================================
	// CHUNK GENERATION
	// ==================================================

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

				AddTriangle(st, a, c, b);
				AddTriangle(st, b, c, d);
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

		// Keep physical terrain for vehicles.
		if (Planet == PlanetType.Rocky ||
			GasHasCollision)
		{
			var shape = mesh.CreateTrimeshShape();

			shape.BackfaceCollision = true;

			var body = new StaticBody3D
			{
				Name = "TerrainBody",
				CollisionLayer = 1,
				CollisionMask = 1
			};

			var collision = new CollisionShape3D
			{
				Name = "TerrainCollision",
				Shape = shape
			};

			body.AddChild(collision);
			chunk.AddChild(body);
		}

		AddChild(chunk);

		_chunks.Add(coord, chunk);
	}

	// ==================================================
	// HEIGHT GENERATION
	// ==================================================

	private Vector3 GetVertex(
		Vector2I coord,
		int x,
		int z)
	{
		int worldX = coord.X * ChunkSize + x;
		int worldZ = coord.Y * ChunkSize + z;

		float y;

		switch (Planet)
		{
			case PlanetType.Gas:
				y = GasSurfaceHeight;
				break;

			case PlanetType.Rocky:
			default:
				y = _noise.GetNoise2D(
					worldX,
					worldZ
				) * HeightScale;
				break;
		}

		return new Vector3(
			x * CellSize,
			y,
			z * CellSize
		);
	}

	private static void AddTriangle(
		SurfaceTool st,
		Vector3 a,
		Vector3 b,
		Vector3 c)
	{
		Vector3 normal =
			(b - a).Cross(c - a).Normalized();

		st.SetNormal(normal);
		st.AddVertex(a);

		st.SetNormal(normal);
		st.AddVertex(b);

		st.SetNormal(normal);
		st.AddVertex(c);
	}

	// ==================================================
	// DEBUG GROUND
	// ==================================================

	private void TestGroundUnderPlayer()
	{
		Vector3 p = ToLocal(Player.GlobalPosition);

		float maxHeight = Planet == PlanetType.Gas
			? Mathf.Abs(GasSurfaceHeight)
			: Mathf.Abs(HeightScale);

		Vector3 from = ToGlobal(
			new Vector3(
				p.X,
				maxHeight + 50f,
				p.Z
			)
		);

		Vector3 to = ToGlobal(
			new Vector3(
				p.X,
				-maxHeight - 50f,
				p.Z
			)
		);

		var query =
			PhysicsRayQueryParameters3D.Create(
				from,
				to
			);

		query.CollisionMask = 1;

		if (Player is CollisionObject3D physicsPlayer)
		{
			query.Exclude =
				new Godot.Collections.Array<Rid>
				{
					physicsPlayer.GetRid()
				};
		}

		var result = GetWorld3D()
			.DirectSpaceState.IntersectRay(query);

		if (result.Count == 0)
		{
			GD.PrintErr(
                "TERRAIN UNDER PLAYER: NO HIT"
			);

			return;
		}

		Vector3 hit =
			(Vector3)result["position"];

		GD.Print(
			$"TERRAIN UNDER PLAYER: {result["collider"]}"
		);

		GD.Print(
			$"Player Y={Player.GlobalPosition.Y:F2}, " +
			$"ground Y={hit.Y:F2}"
		);
	}
}
