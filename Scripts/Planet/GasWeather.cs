using System.Collections.Generic;
using Godot;

// Погода газової планети в сцені поверхні: об'ємний туман, туманні банки за вітром і гроза.
// Художній ефект, не модель атмосфери. Частота блискавок — від тиску атмосфери планети
// (PlanetFx.StormRate, той самий множник, що й у виді системи). На кам'янистих планетах вимикається.
public partial class GasWeather : Node3D
{
	[Export] public TerrainWorld Terrain;
	[Export] public WorldEnvironment Environment;

	[ExportGroup("Туман")]
	[Export] public float VolumetricDensity = 0.012f;
	[Export] public int FogBanks = 10;
	[Export] public float BankRadius = 95f;       // м навколо апарата (у межах видимості туману за глибиною)
	[Export] public float WindSpeed = 4f;         // м/с

	[ExportGroup("Гроза")]
	[Export] public float StrikeMinDistance = 60f;
	[Export] public float StrikeMaxDistance = 260f;
	[Export] public float BaseMinInterval = 3f;    // с, при StormRate = 1
	[Export] public float BaseMaxInterval = 10f;

	private readonly RandomNumberGenerator _rng = new();
	private readonly List<FogVolume> _banks = new();
	private Vector3 _wind;
	private float _rate = 1f;
	private double _nextStrike;
	private ColorRect _flash;
	private bool _active;
	private MeshInstance3D _backdrop; // газова поверхня до обрію (шматки рельєфу — лише навколо апарата)

	private Color _haze = new(0.7f, 0.75f, 0.9f);
	private static readonly Color BoltColor = new(0.85f, 0.92f, 1f);

	public override void _Ready() => CallDeferred(MethodName.Setup);

	private void Setup()
	{
		if (Terrain == null || Terrain.Planet != TerrainWorld.PlanetType.Gas) { QueueFree(); return; }
		_active = true;

		ulong seed = CurrentGen.HasActivePlanet ? (ulong)Mathf.Abs(CurrentGen.ActivePlanet.NoiseSeed) : 4242ul;
		_rng.Seed = seed;
		if (CurrentGen.HasActivePlanet) _rate = PlanetFx.StormRate(CurrentGen.ActivePlanet);
		_haze = Terrain.GasFogColor;
		float angle = _rng.RandfRange(0f, Mathf.Tau);
		_wind = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * WindSpeed;

		// об'ємний туман у кольорі неба; звичайний туман рідшає, щоб не було суцільної пелени
		if (Environment?.Environment is { } env)
		{
			env.VolumetricFogEnabled = true;
			env.VolumetricFogDensity = VolumetricDensity;
			env.VolumetricFogAlbedo = _haze;
			env.VolumetricFogEmission = _haze * 0.04f;
			env.VolumetricFogAnisotropy = 0.55f;
			env.VolumetricFogLength = 240f;
			env.VolumetricFogSkyAffect = 0.5f;
			env.FogSkyAffect = 1f; // небо біля обрію = туман, тож край поверхні не видно
		}

		// Візуальна площина газу до обрію: інакше видно край завантажених шматків.
		// Той самий шейдер і кольори, що й у TerrainWorld; трохи нижче, щоб не мерехтіла з шматками.
		var shader = GD.Load<Shader>("res://Shaders/GasSurface.gdshader");
		if (shader != null)
		{
			var gas = new ShaderMaterial { Shader = shader };
			gas.SetShaderParameter("seed_offset", Terrain.Seed * 0.0137f);
			gas.SetShaderParameter("flow_speed", Terrain.GasFlowSpeed);
			gas.SetShaderParameter("dark_color", Terrain.GasDarkColor);
			gas.SetShaderParameter("mid_color", Terrain.GasMidColor);
			gas.SetShaderParameter("light_color", Terrain.GasLightColor);
			_backdrop = new MeshInstance3D
			{
				Name = "GasBackdrop",
				Mesh = new PlaneMesh { Size = new Vector2(4000f, 4000f), SubdivideWidth = 0, SubdivideDepth = 0 },
				MaterialOverride = gas,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			};
			AddChild(_backdrop);
		}

		var noise = new NoiseTexture3D
		{
			Width = 48, Height = 48, Depth = 48, Seamless = true,
			Noise = new FastNoiseLite { Seed = (int)(seed & 0x7fffffff), Frequency = 0.06f, FractalOctaves = 3 },
		};
		for (int i = 0; i < FogBanks; i++)
		{
			var bank = new FogVolume
			{
				Name = $"FogBank{i}",
				Shape = RenderingServer.FogVolumeShape.Ellipsoid,
				Size = new Vector3(_rng.RandfRange(50f, 95f), _rng.RandfRange(10f, 24f), _rng.RandfRange(50f, 95f)),
				Material = new FogMaterial
				{
					Density = _rng.RandfRange(0.18f, 0.4f),
					Albedo = _haze.Lightened(0.25f),
					DensityTexture = noise,
					EdgeFade = 0.4f,
				},
			};
			AddChild(bank);
			_banks.Add(bank);
			bank.GlobalPosition = RandomAround(Center(), BankRadius);
		}

		var layer = new CanvasLayer { Layer = 4 };
		AddChild(layer);
		_flash = new ColorRect { Color = new Color(BoltColor, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		layer.AddChild(_flash);
		_flash.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		ScheduleStrike();
	}

	private Vector3 Center() => Terrain?.Player != null && IsInstanceValid(Terrain.Player) ? Terrain.Player.GlobalPosition : GlobalPosition;

	private Vector3 RandomAround(Vector3 c, float radius)
	{
		float a = _rng.RandfRange(0f, Mathf.Tau), r = Mathf.Lerp(20f, radius, Mathf.Sqrt(_rng.Randf()));
		float x = c.X + Mathf.Cos(a) * r, z = c.Z + Mathf.Sin(a) * r;
		return new Vector3(x, Terrain.GetHeightAt(x, z) + _rng.RandfRange(3f, 22f), z);
	}

	public override void _Process(double delta)
	{
		if (!_active) return;
		Vector3 c = Center();
		if (_backdrop != null)
			_backdrop.GlobalPosition = new Vector3(c.X, Terrain.GasSurfaceHeight - 0.08f, c.Z);

		// банки пливуть за вітром; занадто далекі переносяться назустріч вітру
		foreach (FogVolume bank in _banks)
		{
			bank.GlobalPosition += _wind * (float)delta;
			Vector3 off = bank.GlobalPosition - c;
			off.Y = 0f;
			if (off.Length() > BankRadius)
			{
				Vector3 upwind = -_wind.Normalized() * BankRadius * 0.9f;
				Vector3 p = c + upwind.Rotated(Vector3.Up, _rng.RandfRange(-0.8f, 0.8f));
				p.Y = Terrain.GetHeightAt(p.X, p.Z) + _rng.RandfRange(3f, 22f);
				bank.GlobalPosition = p;
			}
		}

		_nextStrike -= delta;
		if (_nextStrike <= 0) { Strike(c); ScheduleStrike(); }
	}

	private void ScheduleStrike() =>
		_nextStrike = _rng.RandfRange(BaseMinInterval, BaseMaxInterval) / Mathf.Max(_rate, 0.1f);

	private bool ReducedMotion()
	{
		var settings = GetNodeOrNull<Node>("/root/UserSettings");
		return settings != null && settings.Call("get_value", "reduced_motion").AsBool();
	}

	// ---------- Блискавка ----------

	public void Strike(Vector3 around)
	{
		// здебільшого перед камерою, щоб гроза була видна, а не лише чутна
		float a = _rng.RandfRange(0f, Mathf.Tau);
		if (GetViewport().GetCamera3D() is { } cam && _rng.Randf() < 0.75f)
		{
			Vector3 f = -cam.GlobalBasis.Z;
			a = Mathf.Atan2(f.Z, f.X) + _rng.RandfRange(-1.1f, 1.1f);
		}
		float dist = _rng.RandfRange(StrikeMinDistance, StrikeMaxDistance);
		float x = around.X + Mathf.Cos(a) * dist, z = around.Z + Mathf.Sin(a) * dist;
		var ground = new Vector3(x, Terrain.GetHeightAt(x, z), z);
		var top = ground + new Vector3(_rng.RandfRange(-25f, 25f), _rng.RandfRange(140f, 200f), _rng.RandfRange(-25f, 25f));

		var bolt = new Node3D { Name = "Lightning" };
		AddChild(bolt);
		var mat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = BoltColor,
			EmissionEnabled = true,
			Emission = BoltColor,
			EmissionEnergyMultiplier = 6f,
			DisableFog = true, // розряд яскравий навіть крізь густий туман
		};
		List<Vector3> main = Jagged(top, ground, 16, 9f);
		AddSegments(bolt, main, 0.7f, mat);
		int branches = _rng.RandiRange(1, 3);
		for (int i = 0; i < branches; i++)
		{
			Vector3 from = main[_rng.RandiRange(3, main.Count - 5)];
			Vector3 to = from + new Vector3(_rng.RandfRange(-35f, 35f), -_rng.RandfRange(25f, 60f), _rng.RandfRange(-35f, 35f));
			AddSegments(bolt, Jagged(from, to, 7, 5f), 0.35f, mat);
		}

		bool calm = ReducedMotion();
		var light = new OmniLight3D
		{
			Position = (top + ground) * 0.5f,
			LightColor = BoltColor,
			OmniRange = 420f,
			LightVolumetricFogEnergy = 3f,
			ShadowEnabled = false,
		};
		bolt.AddChild(light);
		light.GlobalPosition = (top + ground) * 0.5f;

		// 2–3 імпульси, як у справжнього розряду; світло підсвічує туман
		float peak = calm ? 4f : 14f;
		var tw = CreateTween();
		tw.TweenProperty(light, "light_energy", peak, 0.03f).From(0f);
		tw.TweenProperty(light, "light_energy", peak * 0.2f, 0.06f);
		tw.TweenProperty(light, "light_energy", peak * 0.8f, 0.04f);
		tw.TweenProperty(light, "light_energy", 0f, 0.25f);
		tw.TweenCallback(Callable.From(bolt.QueueFree));

		if (!calm && _flash != null)
		{
			float near = 1f - Mathf.Clamp((dist - StrikeMinDistance) / (StrikeMaxDistance - StrikeMinDistance), 0f, 1f);
			var ft = CreateTween();
			ft.TweenProperty(_flash, "color:a", 0.05f + 0.13f * near, 0.03f);
			ft.TweenProperty(_flash, "color:a", 0f, 0.35f);
		}
	}

	private List<Vector3> Jagged(Vector3 from, Vector3 to, int segments, float jitter)
	{
		var pts = new List<Vector3> { from };
		for (int i = 1; i < segments; i++)
		{
			float t = (float)i / segments;
			Vector3 p = from.Lerp(to, t);
			float j = jitter * (1f - Mathf.Abs(t - 0.5f));
			pts.Add(p + new Vector3(_rng.RandfRange(-j, j), _rng.RandfRange(-j * 0.3f, j * 0.3f), _rng.RandfRange(-j, j)));
		}
		pts.Add(to);
		return pts;
	}

	private static void AddSegments(Node3D parent, List<Vector3> pts, float width, Material mat)
	{
		for (int i = 0; i + 1 < pts.Count; i++)
		{
			Vector3 a = pts[i], b = pts[i + 1];
			float len = a.DistanceTo(b);
			if (len < 0.01f) continue;
			var seg = new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(width, width, len) },
				MaterialOverride = mat,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			};
			parent.AddChild(seg);
			Vector3 dir = (b - a).Normalized();
			Vector3 up = Mathf.Abs(dir.Y) > 0.95f ? Vector3.Right : Vector3.Up;
			seg.GlobalTransform = new Transform3D(Basis.LookingAt(dir, up), (a + b) * 0.5f);
		}
	}
}
