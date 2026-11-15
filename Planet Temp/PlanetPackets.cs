using Godot;

public static class PlanetPackets
{
	public static void GenerateSkyColors(ref PlanetData d)
	{
		// [1] Seed RNG (offset by +555 so sky colors differ from ground colors)
		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Seed = (ulong)Mathf.Abs(d.NoiseSeed + 555);

		// [2] Base Hue 
		float baseHue = rng.Randf();

		// Dark
		d.SkyDark = Color.FromHsv(baseHue, rng.RandfRange(0.6f, 0.9f), rng.RandfRange(0.08f, 0.22f));

		// Mid
		float midHue = (baseHue + rng.RandfRange(-0.08f, 0.08f) + 1.0f) % 1.0f;
		d.SkyMid = Color.FromHsv(midHue, rng.RandfRange(0.4f, 0.75f), rng.RandfRange(0.35f, 0.55f));

		// Light
		float lightHue = (baseHue + rng.RandfRange(-0.15f, 0.15f) + 1.0f) % 1.0f;
		d.SkyLight = Color.FromHsv(lightHue, rng.RandfRange(0.25f, 0.5f), rng.RandfRange(0.7f, 0.95f));

		// Fog
		d.SkyFog = Color.FromHsv(lightHue, rng.RandfRange(0.1f, 0.35f), rng.RandfRange(0.6f, 0.85f));
	}

	public static void ApplyTexture(Node3D planetNode, PlanetData d)
	{
		// [1] Mesh Check & Seeded RNG
		if (planetNode is not GeometryInstance3D geo) return;

		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Seed = (ulong)Mathf.Abs(d.NoiseSeed);

		Gradient mainGrad = new Gradient();
		Color mainDark = Color.FromHsv(rng.Randf(), rng.RandfRange(0.5f, 0.9f), rng.RandfRange(0.2f, 0.5f));
		Color mainLight = Color.FromHsv(rng.Randf(), rng.RandfRange(0.3f, 0.7f), rng.RandfRange(0.5f, 0.8f));
		mainGrad.SetColor(0, mainDark);
		mainGrad.SetColor(1, mainLight);

		FastNoiseLite mainNoise = new FastNoiseLite();
		mainNoise.Seed = d.NoiseSeed;
		mainNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth;

		NoiseTexture2D mainTex = new NoiseTexture2D();
		mainTex.Noise = mainNoise;
		mainTex.ColorRamp = mainGrad;
		mainTex.Seamless = true;

		StandardMaterial3D mainMat = new StandardMaterial3D();
		mainMat.AlbedoTexture = mainTex;
		mainMat.Uv1Triplanar = true;           
		mainMat.Uv1TriplanarSharpness = 1.0f;   // Smoothly blends the 3D edges


		Color stormColor = Color.FromHsv(rng.Randf(), rng.RandfRange(0.0f, 0.3f), rng.RandfRange(0.85f, 1.0f));

		Gradient cloudGrad = new Gradient();
		// From 0.0 to 0.55 is completely invisible (Alpha = 0) so Layer 1 shows through
		cloudGrad.SetColor(0, new Color(stormColor.R, stormColor.G, stormColor.B, 0.0f));
		cloudGrad.AddPoint(0.55f, new Color(stormColor.R, stormColor.G, stormColor.B, 0.0f));
		cloudGrad.SetColor(2, new Color(stormColor.R, stormColor.G, stormColor.B, 0.85f));

		FastNoiseLite cloudNoise = new FastNoiseLite();
		cloudNoise.Seed = d.NoiseSeed + 999; 
		cloudNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth;
		cloudNoise.DomainWarpEnabled = true; 
		cloudNoise.DomainWarpAmplitude = 30.0f;
		cloudNoise.DomainWarpFrequency = 0.04f;

		NoiseTexture2D cloudTex = new NoiseTexture2D();
		cloudTex.Noise = cloudNoise;
		cloudTex.ColorRamp = cloudGrad;
		cloudTex.Seamless = true;

		StandardMaterial3D cloudMat = new StandardMaterial3D();
		cloudMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		cloudMat.AlbedoTexture = cloudTex;
		cloudMat.Uv1Triplanar = true;
		cloudMat.Uv1TriplanarSharpness = 1.0f;


		if (d.Type == "GasGigant")
		{

			mainNoise.Frequency = 0.001f;
			mainMat.Uv1Scale = new Vector3(1.2f, 6.0f, 1.2f);
			mainMat.Roughness = 1.0f;

			cloudNoise.Frequency = 0.002f;
			cloudMat.Uv1Scale = new Vector3(0.5f, 1.5f, 0.5f);
		}
		else if (d.Type == "Rocky")
		{

			mainNoise.Frequency = 0.02f;
			mainNoise.FractalOctaves = 5;
			mainMat.Uv1Scale = new Vector3(1.0f, 1.0f, 1.0f);
			mainMat.Roughness = 0.85f;

			cloudNoise.Frequency = 0.03f;
			cloudMat.Uv1Scale = new Vector3(0.8f, 0.8f, 0.8f);
		}

		mainMat.NextPass = cloudMat; // Stacks Layer 2 directly over Layer 1
		geo.MaterialOverride = mainMat;
	}
}
