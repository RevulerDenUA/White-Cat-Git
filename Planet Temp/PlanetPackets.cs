using Godot;

public static class PlanetPackets
{
	public static void GenerateSkyColors(ref PlanetData d)
	{
		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Seed = (ulong)Mathf.Abs(d.NoiseSeed + 555);

		float baseHue = rng.Randf();

		d.SkyDark = Color.FromHsv(baseHue, rng.RandfRange(0.6f, 0.9f), rng.RandfRange(0.08f, 0.22f));

		float midHue = (baseHue + rng.RandfRange(-0.08f, 0.08f) + 1.0f) % 1.0f;
		d.SkyMid = Color.FromHsv(midHue, rng.RandfRange(0.4f, 0.75f), rng.RandfRange(0.35f, 0.55f));

		float lightHue = (baseHue + rng.RandfRange(-0.15f, 0.15f) + 1.0f) % 1.0f;
		d.SkyLight = Color.FromHsv(lightHue, rng.RandfRange(0.25f, 0.5f), rng.RandfRange(0.7f, 0.95f));

		d.SkyFog = Color.FromHsv(lightHue, rng.RandfRange(0.1f, 0.35f), rng.RandfRange(0.6f, 0.85f));
	}

	// Основні кольори поверхні — ті самі, що бере ApplyTexture (той самий сід і порядок викликів RNG).
	// Потрібно генератору терену, щоб ґрунт на планеті збігався з виглядом з орбіти.
	public static (Color Dark, Color Light) SurfaceColors(PlanetData d)
	{
		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Seed = (ulong)Mathf.Abs(d.NoiseSeed);
		Color mainDark = Color.FromHsv(rng.Randf(), rng.RandfRange(0.5f, 0.9f), rng.RandfRange(0.2f, 0.5f));
		Color mainLight = Color.FromHsv(rng.Randf(), rng.RandfRange(0.3f, 0.7f), rng.RandfRange(0.5f, 0.8f));
		return (mainDark, mainLight);
	}

	public static void ApplyTexture(Node3D planetNode, PlanetData d)
	{
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
		mainMat.Uv1TriplanarSharpness = 1.0f;

		Color stormColor = Color.FromHsv(rng.Randf(), rng.RandfRange(0.0f, 0.3f), rng.RandfRange(0.85f, 1.0f));

		Gradient cloudGrad = new Gradient();
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

		if (!d.IsMoon && d.AtmospherePressure > 0.0f)
			mainMat.NextPass = cloudMat;

		geo.MaterialOverride = mainMat;
	}

	public static void ApplyParentSkyboxTexture(Node3D skyboxPlanetMesh, ParentPlanetData p)
	{
		PlanetData temp = new PlanetData
		{
			Type = p.Type,
			PlanetSeed = p.PlanetSeed,
			NoiseSeed = p.NoiseSeed,
			AtmospherePressure = p.AtmospherePressure,
			IsMoon = false
		};
		ApplyTexture(skyboxPlanetMesh, temp);
	}

	public static void GenerateChemicals(ref PlanetData d)
	{
		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Seed = (ulong)Mathf.Abs(d.PlanetSeed + 777);

		if (d.Type == "GasGigant")
		{
			d.AtmospherePressure = rng.RandfRange(80.0f, 950.0f);
			d.MainGas = "Hydrogen";

			if (d.Temperature < 45.0f)
			{
				d.AtmoTemplate = "Cryo-Gas";
				d.Hydrogen = rng.RandfRange(65.0f, 78.0f);
				d.Helium = rng.RandfRange(15.0f, 25.0f);
				d.Methane = rng.RandfRange(3.0f, 10.0f);
				d.Ammonia = rng.RandfRange(0.5f, 3.0f);
			}
			else
			{
				d.AtmoTemplate = "Hydrogen-Helium";
				d.Hydrogen = rng.RandfRange(75.0f, 88.0f);
				d.Helium = rng.RandfRange(10.0f, 22.0f);
				d.Methane = rng.RandfRange(0.5f, 2.5f);
				d.Ammonia = rng.RandfRange(0.2f, 1.5f);
			}

			d.CoreTemplate = "Compressed-Core";
			d.MainSolid = "Silicates";
			d.Silicates = rng.RandfRange(40.0f, 55.0f);
			d.Iron = rng.RandfRange(20.0f, 35.0f);
			d.WaterIce = rng.RandfRange(15.0f, 30.0f);
			d.Carbon = rng.RandfRange(2.0f, 8.0f);
		}
		else if (d.Type == "Rocky")
		{
			if (d.IsMoon || rng.Randf() < 0.20f)
			{
				d.AtmospherePressure = 0.0f;
				d.AtmoTemplate = "Vacuum";
				d.MainGas = "None";
			}
			else if (d.Temperature > 75.0f)
			{
				d.AtmospherePressure = rng.RandfRange(15.0f, 90.0f);
				d.AtmoTemplate = "Dense-Sulfuric";
				d.MainGas = "CarbonDioxide";
				d.CarbonDioxide = rng.RandfRange(85.0f, 96.0f);
				d.Nitrogen = rng.RandfRange(2.0f, 10.0f);
				d.SulfurDioxide = rng.RandfRange(1.0f, 6.0f);
			}
			else if (d.Temperature < 45.0f)
			{
				d.AtmospherePressure = rng.RandfRange(0.5f, 4.5f);
				d.AtmoTemplate = "Methane-Haze";
				d.MainGas = "Nitrogen";
				d.Nitrogen = rng.RandfRange(65.0f, 85.0f);
				d.Methane = rng.RandfRange(12.0f, 30.0f);
				d.Hydrogen = rng.RandfRange(0.5f, 3.0f);
			}
			else
			{
				d.AtmospherePressure = rng.RandfRange(0.1f, 2.5f);
				d.AtmoTemplate = "Nitrogen-Oxygen";
				d.MainGas = "Nitrogen";
				d.Nitrogen = rng.RandfRange(70.0f, 82.0f);
				d.Oxygen = rng.RandfRange(12.0f, 24.0f);
				d.CarbonDioxide = rng.RandfRange(1.0f, 5.0f);
				d.Methane = rng.RandfRange(0.2f, 1.5f);
			}

			if (d.Temperature > 70.0f)
			{
				d.CoreTemplate = "Iron-Rich";
				d.MainSolid = "Iron";
				d.Iron = rng.RandfRange(50.0f, 68.0f);
				d.Silicates = rng.RandfRange(25.0f, 40.0f);
				d.Sulfur = rng.RandfRange(5.0f, 15.0f);
			}
			else if (d.Temperature < 45.0f)
			{
				d.CoreTemplate = "Cryo-Rock";
				d.MainSolid = "WaterIce";
				d.WaterIce = rng.RandfRange(45.0f, 65.0f);
				d.Silicates = rng.RandfRange(25.0f, 40.0f);
				d.Iron = rng.RandfRange(8.0f, 18.0f);
				d.Carbon = rng.RandfRange(2.0f, 8.0f);
			}
			else
			{
				d.CoreTemplate = "Silicate-Core";
				d.MainSolid = "Silicates";
				d.Silicates = rng.RandfRange(50.0f, 68.0f);
				d.Iron = rng.RandfRange(22.0f, 35.0f);
				d.Carbon = rng.RandfRange(3.0f, 10.0f);
				d.Sulfur = rng.RandfRange(1.0f, 6.0f);
			}
		}

		float gasTotal = d.Hydrogen + d.Helium + d.Nitrogen + d.Oxygen +
						 d.CarbonDioxide + d.Methane + d.Ammonia + d.SulfurDioxide;

		if (gasTotal > 0.001f)
		{
			float gasScale = 100.0f / gasTotal;
			d.Hydrogen = Mathf.Round(d.Hydrogen * gasScale * 10f) * 0.1f;
			d.Helium = Mathf.Round(d.Helium * gasScale * 10f) * 0.1f;
			d.Nitrogen = Mathf.Round(d.Nitrogen * gasScale * 10f) * 0.1f;
			d.Oxygen = Mathf.Round(d.Oxygen * gasScale * 10f) * 0.1f;
			d.CarbonDioxide = Mathf.Round(d.CarbonDioxide * gasScale * 10f) * 0.1f;
			d.Methane = Mathf.Round(d.Methane * gasScale * 10f) * 0.1f;
			d.Ammonia = Mathf.Round(d.Ammonia * gasScale * 10f) * 0.1f;
			d.SulfurDioxide = Mathf.Round(d.SulfurDioxide * gasScale * 10f) * 0.1f;
		}

		float solidTotal = d.Iron + d.Silicates + d.WaterIce + d.Carbon + d.Sulfur;
		if (solidTotal > 0.001f)
		{
			float solidScale = 100.0f / solidTotal;
			d.Iron = Mathf.Round(d.Iron * solidScale * 10f) * 0.1f;
			d.Silicates = Mathf.Round(d.Silicates * solidScale * 10f) * 0.1f;
			d.WaterIce = Mathf.Round(d.WaterIce * solidScale * 10f) * 0.1f;
			d.Carbon = Mathf.Round(d.Carbon * solidScale * 10f) * 0.1f;
			d.Sulfur = Mathf.Round(d.Sulfur * solidScale * 10f) * 0.1f;
		}
	}
}
