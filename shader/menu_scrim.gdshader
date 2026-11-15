shader_type canvas_item;
uniform float settings_amount : hint_range(0.0, 1.0) = 0.0;
void fragment() {
	float left = 1.0 - smoothstep(0.0, 0.62, UV.x);
	float edge = pow(abs(UV.y - 0.5) * 2.0, 3.0) * 0.22;
	float shade = left * 0.70 + edge;
	COLOR = vec4(0.018, 0.028, 0.045, min(0.96, shade + settings_amount * 0.84));
}
