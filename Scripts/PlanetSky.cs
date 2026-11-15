
using Godot;

public static class PlanetSky
{
	public static void ApplySkybox(
		Godot.Environment env,
		Color skyDark,
		Color skyMid,
		Color skyFog,
		Color skyLight)
	{
		if (env == null)
			return;

		const string shaderPath =
			"res://Shaders/PlanetSkyGradient.gdshader";

		var shader = GD.Load<Shader>(shaderPath);

		if (shader == null)
		{
			GD.PushError(
				$"PlanetSky: Missing shader: {shaderPath}"
			);
			return;
		}

		// Four-color gradient
		var gradient = new Gradient();

		gradient.SetColor(0, skyDark);
		gradient.SetColor(1, skyLight);

		gradient.AddPoint(0.4f, skyMid);
		gradient.AddPoint(0.75f, skyFog);

		var texture = new GradientTexture1D
		{
			Gradient = gradient,
			Width = 512
		};

		var material = new ShaderMaterial
		{
			Shader = shader
		};

		material.SetShaderParameter(
			"gradient_texture", texture
		);

		env.BackgroundMode =
			Godot.Environment.BGMode.Sky;

		env.Sky = new Sky
		{
			SkyMaterial = material
		};

		// TerrainWorld controls the fog and storms.
	}
}
