shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, shadows_disabled, fog_disabled, cull_disabled;
void fragment() {
	float radius = length(UV - vec2(0.5));
	float halo = exp(-radius * 19.0) * 0.22 * (1.0 - smoothstep(0.30, 0.48, radius));
	float disc = 1.0 - smoothstep(0.004, 0.012, radius);
	ALBEDO = vec3(1.0, 0.70, 0.38) * halo + vec3(1.0, 0.95, 0.8) * disc * 4.0;
	ALPHA = 1.0;
}
