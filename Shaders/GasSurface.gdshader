shader_type spatial;
render_mode cull_disabled, depth_draw_opaque;

uniform vec4 dark_color : source_color = vec4(0.10, 0.16, 0.32, 1.0);
uniform vec4 mid_color : source_color = vec4(0.24, 0.49, 0.64, 1.0);
uniform vec4 light_color : source_color = vec4(0.70, 0.83, 0.80, 1.0);
uniform float flow_speed : hint_range(0.0, 3.0) = 0.4;
uniform float seed_offset = 0.0;
uniform float storm_strength : hint_range(0.0, 1.0) = 1.0;

varying vec2 world_xz;

float hash21(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float value_noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);

    float a = hash21(i);
    float b = hash21(i + vec2(1.0, 0.0));
    float c = hash21(i + vec2(0.0, 1.0));
    float d = hash21(i + vec2(1.0, 1.0));

    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm(vec2 p) {
    float result = 0.0;
    float amplitude = 0.5;
    for (int i = 0; i < 4; i++) {
        result += amplitude * value_noise(p);
        p = p * 2.03 + vec2(3.7, 1.4);
        amplitude *= 0.5;
    }
    return result;
}

void vertex() {
    // This world-space coordinate keeps the pattern seamless across chunks.
    world_xz = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

void fragment() {
    float time = TIME * flow_speed;
    vec2 p = world_xz * 0.033 + vec2(seed_offset, -seed_offset);

    float large_cloud = fbm(p * 0.7 + vec2(time * 0.04, -time * 0.07));
    // Continuous gas-giant storms. Stable world-space vortex centres prevent chunk seams.
    vec2 cell_id = floor(p / 3.0);
    vec2 storm_center = (cell_id + vec2(
        0.2 + 0.6 * hash21(cell_id + vec2(11.7, 2.6)),
        0.2 + 0.6 * hash21(cell_id + vec2(5.8, 9.1)))) * 3.0;
    vec2 relative = p - storm_center;
    float radius = length(relative);
    float vortex = exp(-radius * radius * 1.4);
    float angle = storm_strength * vortex * (3.6 + 0.15 * time);
	float c = cos(angle);
	float s = sin(angle);

	mat2 rotation = mat2(
    	vec2(c, s),
    	vec2(-s, c)
	);

vec2 spun = rotation * relative;
    vec2 warped = storm_center + spun + vec2(large_cloud * 1.65, -large_cloud * 0.8);

    float swirls = fbm(warped * 2.0 + vec2(time * 0.20, -time * 0.12));
    float stripe = sin((warped.y + swirls * 0.52) * 7.0 + sin(warped.x * 1.2) * 0.4);
    float storm_rings = 0.5 + 0.5 * sin(radius * 15.0 - time * 1.4 + atan(spun.y, spun.x) * 2.0);
    float storm_band = storm_strength * vortex;
    float pattern = clamp(0.48 * swirls + 0.37 * (stripe * 0.5 + 0.5)
        + 0.15 * large_cloud - 0.32 * storm_band
        + 0.18 * storm_band * storm_rings, 0.0, 1.0);

    vec3 color = mix(dark_color.rgb, mid_color.rgb, smoothstep(0.20, 0.67, pattern));
    color = mix(color, light_color.rgb, smoothstep(0.60, 0.88, pattern));

    ALBEDO = color;
    METALLIC = 0.05;
    ROUGHNESS = mix(0.46, 0.25, pattern);
    SPECULAR = 0.7;
    EMISSION = light_color.rgb * (
        pow(smoothstep(0.76, 0.98, pattern), 3.0) * 0.07
        + storm_band * pow(storm_rings, 12.0) * 0.1);
}