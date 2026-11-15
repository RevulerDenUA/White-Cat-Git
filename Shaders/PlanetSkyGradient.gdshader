
shader_type sky;

uniform vec4 sky_dark : source_color =
    vec4(0.13, 0.17, 0.28, 1.0);

uniform vec4 sky_mid : source_color =
    vec4(0.35, 0.49, 0.70, 1.0);

uniform vec4 sky_fog : source_color =
    vec4(0.59, 0.69, 0.76, 1.0);

uniform vec4 sky_light : source_color =
    vec4(0.76, 0.85, 0.95, 1.0);

uniform float daylight : hint_range(0.0, 1.0) = 1.0;
uniform vec3 sun_direction = vec3(0.0, 1.0, 0.0);

void sky()
{
    float h = clamp(EYEDIR.y, 0.0, 1.0);

    vec3 day_color;

    if (h < 0.4)
    {
        day_color = mix(
            sky_dark.rgb,
            sky_mid.rgb,
            smoothstep(0.0, 0.4, h)
        );
    }
    else if (h < 0.75)
    {
        day_color = mix(
            sky_mid.rgb,
            sky_fog.rgb,
            smoothstep(0.4, 0.75, h)
        );
    }
    else
    {
        day_color = mix(
            sky_fog.rgb,
            sky_light.rgb,
            smoothstep(0.75, 1.0, h)
        );
    }

    vec3 night_color = mix(
        vec3(0.005, 0.008, 0.025),
        vec3(0.015, 0.025, 0.070),
        h
    );

    vec3 color = mix(
        night_color,
        day_color,
        daylight
    );

    // Sun disk and soft glow
    float sun_dot = dot(
        normalize(EYEDIR),
        normalize(sun_direction)
    );

    float sun_disk = smoothstep(
        0.9995,
        0.9998,
        sun_dot
    );

    float sun_glow = pow(
        max(sun_dot, 0.0),
        180.0
    ) * 0.5;

    vec3 sun_color = vec3(1.0, 0.75, 0.45);

    color += sun_color *
        (sun_disk * 2.5 + sun_glow) *
        daylight;

    COLOR = color;
}
