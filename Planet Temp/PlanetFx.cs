using Godot;

// Візуальні ефекти тіл у виді системи (телескоп / сцена system).
// Газові гіганти: димка по краю диска й спалахи блискавок у хмарах — художній ефект,
// не модель реальної атмосфери. Частота спалахів зростає з тиском атмосфери планети.
public static class PlanetFx
{
	public const string NodeName = "GasFx";

	private const string GasShaderCode = @"
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_back, shadows_disabled;
uniform vec4 haze_color : source_color = vec4(0.6, 0.7, 0.9, 1.0);
uniform vec4 flash_color : source_color = vec4(0.82, 0.9, 1.0, 1.0);
uniform float haze_strength = 1.3;
uniform float storm_rate = 1.0;
uniform float seed = 0.0;
uniform float cells = 3.0;
varying vec3 obj;
float h(vec3 p) { return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453); }
void vertex() { obj = VERTEX; }
void fragment() {
	float ndv = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	vec3 col = haze_color.rgb * pow(1.0 - ndv, 3.0) * haze_strength;
	vec3 p = normalize(obj) * cells;
	vec3 id = floor(p);
	vec3 centre = vec3(h(id + seed), h(id + seed + 1.7), h(id + seed + 3.1));
	float d = length(fract(p) - centre);
	float t = TIME * storm_rate * 0.5 + h(id + seed + 9.0) * 37.0;
	float on = step(0.6, h(id + floor(t) + seed));
	float ph = fract(t);
	float pulse = exp(-ph * 18.0) + 0.6 * exp(-abs(ph - 0.12) * 40.0);
	col += flash_color.rgb * on * pulse * smoothstep(0.75, 0.0, d) * 4.0 * (0.35 + 0.65 * ndv);
	ALBEDO = col;
}";

	private static Shader _gasShader;

	// Викликається після ApplyTexture. Знімає старий ефект (клони дублюють дітей оригіналу)
	// і додає новий, якщо тіло — газовий гігант.
	public static void Apply(Node3D planetNode, PlanetData d)
	{
		if (planetNode == null) return;
		if (planetNode.GetNodeOrNull(NodeName) is Node old)
		{
			planetNode.RemoveChild(old);
			old.QueueFree();
		}
		if (d.IsMoon || d.Type != "GasGigant") return;

		_gasShader ??= new Shader { Code = GasShaderCode };
		var mat = new ShaderMaterial { Shader = _gasShader };
		Color haze = d.SkyLight.A > 0f ? d.SkyLight : new Color(0.7f, 0.8f, 0.95f);
		mat.SetShaderParameter("haze_color", haze);
		mat.SetShaderParameter("seed", (d.NoiseSeed & 0xffff) * 0.01f);
		mat.SetShaderParameter("storm_rate", StormRate(d));

		planetNode.AddChild(new MeshInstance3D
		{
			Name = NodeName,
			Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 48, Rings = 24 },
			Scale = Vector3.One * 1.045f, // трохи над хмарами
			MaterialOverride = mat,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		});
	}

	// 0.5…2: що щільніша атмосфера, то частіші спалахи (той самий множник — на поверхні)
	public static float StormRate(PlanetData d)
	{
		float t = Mathf.Clamp((d.AtmospherePressure - 80f) / 870f, 0f, 1f);
		return Mathf.Lerp(0.5f, 2f, t);
	}
}
