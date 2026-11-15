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

	// Шар 1 — машини, шар 2 — «земля» для бурів, ніжок бура й Інжінюїті (collision_mask = 2).
	[Export(PropertyHint.Layers3DPhysics)] public uint TerrainCollisionLayer = 1u | 2u;

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
	[Export] public bool HideTerrainEdge = true;      // туман за глибиною до межі шматків
	[Export(PropertyHint.Range, "0,0.9,0.05")] public float FogEdgeStart = 0.3f; // частка відстані до краю, де туман починається
	[Export] public WorldEnvironment FogEnvironment;

	private FastNoiseLite _noise;
	private Material _material;
	private bool _initialized;

	// Кольори ґрунту з даних планети (ті самі, що в текстури планети на орбіті)
	private bool _hasPlanetGround;
	private Color _groundDark, _groundLight;

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

		// Дані планети й шум (може вже бути зроблено раніше через GetHeightAt)
		EnsureInitialized();

		switch (Planet)
		{
			case PlanetType.Rocky:
				_material = CreateRockyMaterial();
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

	// Дані планети й шум висот. Викликається з _Ready або раніше — з GetHeightAt
	// (дочірні вузли, напр. OreGenerator, отримують _Ready раніше за корінь-терен).
	private void EnsureInitialized()
	{
		if (_initialized) return;
		_initialized = true;

		// Load generated planet data before
		// initializing noise and materials.
		ApplyActivePlanet();

		_noise = new FastNoiseLite
		{
			Seed = Seed,
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = NoiseFrequency
		};
	}

	// ==================================================
	// SURFACE HEIGHT
	// ==================================================

	// Точна висота згенерованої поверхні (глобальні координати) — та сама тріангуляція,
	// що й у GenerateChunk: клітинка a(0,0) b(1,0) c(0,1) d(1,1), трикутники a→b→c і b→d→c.
	public float GetHeightAt(float worldX, float worldZ)
	{
		EnsureInitialized();

		Vector3 local = IsInsideTree()
			? ToLocal(new Vector3(worldX, 0f, worldZ))
			: new Vector3(worldX, 0f, worldZ);

		float h;
		if (Planet == PlanetType.Gas || CellSize <= 0f)
		{
			h = GasSurfaceHeight;
		}
		else
		{
			float gx = local.X / CellSize;
			float gz = local.Z / CellSize;
			int ix = Mathf.FloorToInt(gx);
			int iz = Mathf.FloorToInt(gz);
			float fx = gx - ix;
			float fz = gz - iz;

			float ha = SampleHeight(ix, iz);
			float hb = SampleHeight(ix + 1, iz);
			float hc = SampleHeight(ix, iz + 1);

			if (fx + fz <= 1f)
			{
				h = ha + (hb - ha) * fx + (hc - ha) * fz;
			}
			else
			{
				float hd = SampleHeight(ix + 1, iz + 1);
				h = hd + (hc - hd) * (1f - fx) + (hb - hd) * (1f - fz);
			}
		}

		if (!IsInsideTree()) return h;
		return ToGlobal(new Vector3(local.X, h, local.Z)).Y;
	}

	// Висота вершини сітки (індекси — як worldX/worldZ у GetVertex)
	private float SampleHeight(int gridX, int gridZ)
	{
		return Planet == PlanetType.Gas
			? GasSurfaceHeight
			: _noise.GetNoise2D(gridX, gridZ) * HeightScale;
	}

	// Ґрунт кам'янистої планети: шум у кольорах планети з орбіти,
	// без активної планети — колишній рівний колір.
	private Material CreateRockyMaterial()
	{
		if (!_hasPlanetGround)
		{
			return new StandardMaterial3D
			{
				AlbedoColor =
					new Color(0.35f, 0.65f, 0.3f),

				Roughness = 1f,

				CullMode =
					BaseMaterial3D.CullModeEnum.Disabled
			};
		}

		var ramp = new Gradient();
		ramp.SetColor(0, _groundDark);
		ramp.SetColor(1, _groundLight);

		var texture = new NoiseTexture2D
		{
			Noise = new FastNoiseLite
			{
				Seed = Seed,
				NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
				Frequency = 0.02f,
				FractalOctaves = 5
			},
			ColorRamp = ramp,
			Seamless = true
		};

		return new StandardMaterial3D
		{
			AlbedoTexture = texture,
			Uv1Triplanar = true,
			Uv1WorldTriplanar = true,
			Uv1Scale = Vector3.One * 0.02f,
			Roughness = 1f,

			CullMode =
				BaseMaterial3D.CullModeEnum.Disabled
		};
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
		// Генератор системи дає "Rocky" / "GasGigant" (місяці — "Rocky" з IsMoon)
		switch (p.Type?.Trim().ToLowerInvariant())
		{
			case "gas":
			case "gasgigant":
			case "gasgiant":
			case "gas_giant":
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

		// Колір ґрунту — як текстура планети з орбіти
		(_groundDark, _groundLight) = PlanetPackets.SurfaceColors(p);
		_hasPlanetGround = true;

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

		// Край завантаженого рельєфу (шматки лише навколо апарата) ховаємо туманом за глибиною:
		// до FogEdgeStart картинка чиста, до межі шматків — повністю в тумані.
		if (HideTerrainEdge)
		{
			// найближчий край шматків — RenderDistance шматків від апарата (коли він на межі свого шматка)
			float edge = RenderDistance * ChunkSize * CellSize;
			env.FogMode = Godot.Environment.FogModeEnum.Depth;
			env.FogDensity = 4f; // у режимі за глибиною це множник: 4 дає повне закриття на FogDepthEnd
			env.FogDepthBegin = edge * FogEdgeStart;
			env.FogDepthEnd = edge * 0.9f;
			env.FogDepthCurve = 1.6f;
		}

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

		// Keep physical terrain for vehicles.
		if (Planet == PlanetType.Rocky ||
			GasHasCollision)
		{
			var shape = mesh.CreateTrimeshShape();

			shape.BackfaceCollision = true;

			var body = new StaticBody3D
			{
				Name = "TerrainBody",
				CollisionLayer = TerrainCollisionLayer,
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
		// при порядку за годинниковою (як у Godot) нормаль лицевої сторони = (c - a) × (b - a)
		Vector3 normal = (c - a).Cross(b - a).Normalized();
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