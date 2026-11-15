using Godot;

public static class PlanetPackets
{
	public static void ApplyTexture(Node3D planetNode, PlanetData d)
	{
		// [1] Mesh Check & Seeded RNG
		if (planetNode is not GeometryInstance3D geo) return;

		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Seed = (ulong)Mathf.Abs(d.NoiseSeed);

		// ==========================================
		// [2] LAYER 1: MAIN SURFACE (Colors & Noise)
		// ==========================================
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
		mainMat.Uv1Triplanar = true;            // Fixes the seam line and pole pinching
		mainMat.Uv1TriplanarSharpness = 1.0f;   // Smoothly blends the 3D edges

		// ==========================================
		// [3] LAYER 2: CLOUDS / TORNADOES (Alpha & Swirl)
		// ==========================================
		Color stormColor = Color.FromHsv(rng.Randf(), rng.RandfRange(0.0f, 0.3f), rng.RandfRange(0.85f, 1.0f));

		Gradient cloudGrad = new Gradient();
		// From 0.0 to 0.55 is completely invisible (Alpha = 0) so Layer 1 shows through
		cloudGrad.SetColor(0, new Color(stormColor.R, stormColor.G, stormColor.B, 0.0f));
		cloudGrad.AddPoint(0.55f, new Color(stormColor.R, stormColor.G, stormColor.B, 0.0f));
		// From 0.55 to 1.0 fades in the clouds/tornadoes (Alpha = 0.85)
		cloudGrad.SetColor(2, new Color(stormColor.R, stormColor.G, stormColor.B, 0.85f));

		FastNoiseLite cloudNoise = new FastNoiseLite();
		cloudNoise.Seed = d.NoiseSeed + 999; // Offset seed so clouds don't copy the ground
		cloudNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth;
		cloudNoise.DomainWarpEnabled = true; // Twists noise into tornadoes/storms
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

		// ==========================================
		// [4] PLANET TYPE PRESETS (Copy & Tweak for New Types)
		// ==========================================
		if (d.Type == "GasGigant")
		{
			// Stretches Y axis to create horizontal gas bands all around the sphere
			mainNoise.Frequency = 0.001f;
			mainMat.Uv1Scale = new Vector3(1.2f, 6.0f, 1.2f);
			mainMat.Roughness = 1.0f;

			// Swirling storm bands & hurricanes
			cloudNoise.Frequency = 0.002f;
			cloudMat.Uv1Scale = new Vector3(0.5f, 1.5f, 0.5f);
		}
		else if (d.Type == "Rocky")
		{
			// Equal X, Y, Z scale for continents and craters
			mainNoise.Frequency = 0.02f;
			mainNoise.FractalOctaves = 5;
			mainMat.Uv1Scale = new Vector3(1.0f, 1.0f, 1.0f);
			mainMat.Roughness = 0.85f;

			// Fluffy swirling cloud/tornado layer
			cloudNoise.Frequency = 0.03f;
			cloudMat.Uv1Scale = new Vector3(0.8f, 0.8f, 0.8f);
		}

		// ==========================================
		// [5] LINK LAYERS & APPLY TO PLANET
		// ==========================================
		mainMat.NextPass = cloudMat; // Stacks Layer 2 directly over Layer 1
		geo.MaterialOverride = mainMat;
	}
}
