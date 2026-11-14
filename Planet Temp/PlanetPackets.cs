using Godot;
using System;

public partial class PlanetPackets : Node
{
public static void ApplyTexture(Node3D planetNode, PlanetData d)
	{
		// [1] Mesh Cast
		if (planetNode is not GeometryInstance3D geo) return;

		// [2] Seeded Colors
		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Seed = (ulong)Mathf.Abs(d.NoiseSeed);

		Gradient grad = new Gradient();
		Color baseColor = Color.FromHsv(rng.Randf(), rng.RandfRange(0.4f, 0.8f), rng.RandfRange(0.3f, 0.6f));
		Color highColor = Color.FromHsv(rng.Randf(), rng.RandfRange(0.2f, 0.7f), rng.RandfRange(0.6f, 0.9f));
		grad.SetColor(0, baseColor);
		grad.SetColor(1, highColor);

		// [3] Noise Setup
		FastNoiseLite noise = new FastNoiseLite();
		noise.Seed = d.NoiseSeed;
		noise.NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth;

		// [4] Texture Generation
		NoiseTexture2D tex = new NoiseTexture2D();
		tex.Noise = noise;
		tex.ColorRamp = grad;
		tex.Seamless = true;

		// [5] Material & Type Rules
		StandardMaterial3D mat = new StandardMaterial3D();
		mat.AlbedoTexture = tex;

		if (d.Type == "GasGigant")
		{
			noise.Frequency = 0.015f;
			mat.Uv1Scale = new Vector3(0.1f, 5.0f, 1.0f);
			mat.Roughness = 1.0f;
		}
		else // Rocky
		{
			noise.Frequency = 0.035f;
			noise.FractalOctaves = 5;
			mat.Roughness = 0.8f;
		}

		geo.MaterialOverride = mat;
	}
}
