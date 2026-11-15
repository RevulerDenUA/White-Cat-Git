shader_type spatial;
render_mode unshaded;
uniform sampler2D earth_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform vec3 sun_direction = vec3(-0.7, 0.6, -0.35);
varying vec3 world_normal;
void vertex() {
	world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}
void fragment() {
	vec3 base = texture(earth_texture, UV).rgb;
	float day = smoothstep(-0.18, 0.8, dot(normalize(world_normal), normalize(sun_direction)));
	float fresnel = pow(1.0 - max(dot(NORMAL, VIEW), 0.0), 3.0);
	ALBEDO = base * mix(0.012, 0.46, day) + vec3(0.075, 0.24, 0.45) * fresnel * day * 0.45;
}
