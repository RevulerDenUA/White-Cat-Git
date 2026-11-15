
using Godot;

public partial class SunCycle : DirectionalLight3D
{
	[ExportGroup("Time")]
	[Export] public float CycleSeconds = 600f;
	[Export] public float StartHour = 12f;

	[ExportGroup("Sun")]
	[Export] public float DaySunEnergy = 1.5f;
	[Export] public float SunAzimuth = 0f;

	[ExportGroup("Environment")]
	[Export] public WorldEnvironment WorldEnv;
	[Export] public float NightBrightness = 0.25f;

	private double _elapsed = 0.0;

	public override void _Ready()
	{
		ShadowEnabled = true;
		UpdateSun();
	}

	public override void _Process(double delta)
	{
		_elapsed += delta;

		if (CycleSeconds > 0f)
			_elapsed %= CycleSeconds;

		UpdateSun();
	}

	private void UpdateSun()
	{
		float duration = Mathf.Max(CycleSeconds, 1f);

		float time = (
			StartHour / 24f +
			(float)(_elapsed / duration)
		) % 1f;

		if (time < 0f)
			time += 1f;

		// Midnight = 0.0
		// Sunrise = 0.25
		// Noon = 0.5
		// Sunset = 0.75

		float angle = (time - 0.5f) * 360f - 90f;

		RotationDegrees = new Vector3(
			angle,
			SunAzimuth,
			0f
		);

		// -1 at midnight, +1 at noon
		float altitude = -Mathf.Cos(
			time * Mathf.Pi * 2f
		);

		// Smooth day/night transition
		float daylight = Mathf.Clamp(
			(altitude + 0.10f) / 0.35f,
			0f,
			1f
		);

		daylight = daylight * daylight *
			(3f - 2f * daylight);

		LightEnergy = DaySunEnergy * daylight;

		// Warmer light near the horizon
		float sunset = 1f - Mathf.Clamp(
			Mathf.Abs(altitude) / 0.35f,
			0f,
			1f
		);

		LightColor = new Color(
			1f,
			0.98f - sunset * 0.30f,
			0.90f - sunset * 0.42f
		);

		UpdateEnvironment(daylight);
	}

	private void UpdateEnvironment(float daylight)
	{
		// Fetch every frame because TerrainWorld may
		// replace the Environment resource in _Ready().
		Godot.Environment env = WorldEnv != null
			? WorldEnv.Environment
			: GetWorld3D().Environment;

		if (env == null)
			return;

		// Darken the whole rendered scene at night
		env.AdjustmentEnabled = true;

		env.AdjustmentBrightness = Mathf.Lerp(
			Mathf.Clamp(NightBrightness, 0f, 1f),
			1f,
			daylight
		);

		// Ambient light
		env.AmbientLightEnergy = Mathf.Lerp(
			0.12f,
			1f,
			daylight
		);

		// Update our custom planet sky
		if (env.Sky?.SkyMaterial is ShaderMaterial sky)
		{
			sky.SetShaderParameter(
				"daylight",
				daylight
			);

			sky.SetShaderParameter(
				"sun_direction",
				GlobalBasis.Z.Normalized()
			);
		}
	}
}
