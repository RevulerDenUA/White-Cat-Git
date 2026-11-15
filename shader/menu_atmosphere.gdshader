shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, shadows_disabled;
void fragment() {
	float rim = pow(1.0 - max(dot(NORMAL, VIEW), 0.0), 5.0);
	ALBEDO = vec3(0.08, 0.32, 0.65);
	ALPHA = rim * 0.30;
}
