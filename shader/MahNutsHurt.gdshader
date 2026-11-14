// Камера марсохода (ч/б або кольорова) з перешкодами і "просіданням бітрейту":
// зерно, сканлайни, тремтіння рядків, смуга перешкод, сніг,
// а час від часу — пікселізація макроблоками, з'їхавші блоки і бендинг, як у стисненому відео.
shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;

group_uniforms Image;
uniform bool color_mode = false;                               // true — кольорова камера, false — ч/б
uniform float chroma_shift : hint_range(0.0, 20.0) = 6.0;      // розїзд RGB при поганому сигналі (кольоровий режим), px
uniform float contrast : hint_range(0.5, 2.5) = 1.35;
uniform float brightness : hint_range(-0.5, 0.5) = 0.02;
uniform vec4 tint : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float vignette_strength : hint_range(0.0, 2.0) = 0.9;

group_uniforms Noise;
uniform float grain_amount : hint_range(0.0, 0.5) = 0.1;
uniform float scanline_strength : hint_range(0.0, 0.5) = 0.07;
uniform float scanline_density : hint_range(0.1, 2.0) = 0.7;
uniform float flicker_amount : hint_range(0.0, 0.3) = 0.04;
uniform float line_jitter : hint_range(0.0, 20.0) = 1.2;       // постійне тремтіння рядків, px
uniform float band_strength : hint_range(0.0, 1.0) = 0.35;     // смуга перешкод, що повзе екраном
uniform float band_speed : hint_range(0.0, 1.0) = 0.12;

group_uniforms BitrateDrop;
uniform float signal_quality : hint_range(0.0, 1.0) = 1.0;     // 1 — ідеальний сигнал (керуй з коду)
uniform float forced_drop : hint_range(0.0, 1.0) = 0.0;        // разовий глітч (напр. при ударі)
uniform bool auto_drops = true;                                // випадкові просідання самі по собі
uniform float drop_chance : hint_range(0.0, 1.0) = 0.12;       // шанс просідання на кожен інтервал
uniform float drop_interval : hint_range(0.05, 2.0) = 0.35;    // довжина інтервалу, с
uniform float max_block_size : hint_range(2.0, 64.0) = 24.0;   // найбільший "піксель", px
uniform float macroblock_size : hint_range(8.0, 128.0) = 48.0; // зона, що ламається цілком, px
uniform float block_shift : hint_range(0.0, 40.0) = 10.0;      // наскільки з'їжджають зламані блоки, px
uniform float min_gray_levels : hint_range(2.0, 64.0) = 6.0;   // відтінків сірого при найгіршому сигналі

group_uniforms Reticle;
uniform float reticle_size = 18.0;
uniform float reticle_gap = 5.0;
uniform float reticle_alpha : hint_range(0.0, 1.0) = 0.55;
group_uniforms;

// Хеш, стабільний і для великих чисел
float hash(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

// Сила автоматичного просідання в момент часу t (0..1), з плавним "хвостом"
float auto_drop_at(float slot, float t) {
	float now = step(hash(vec2(slot, 3.1)), drop_chance) * (0.35 + 0.65 * hash(vec2(slot, 7.7)));
	float prev = step(hash(vec2(slot - 1.0, 3.1)), drop_chance) * (0.35 + 0.65 * hash(vec2(slot - 1.0, 7.7)));
	return max(now, prev * (1.0 - t) * 0.6);
}

void fragment() {
	vec2 px = SCREEN_PIXEL_SIZE;
	vec2 frag = SCREEN_UV / px;          // піксельні координати
	float fast = floor(TIME * 30.0);     // "кадр" для швидкого шуму
	float slot = floor(TIME / drop_interval);

	// ── 1. Наскільки зараз поганий сигнал ──
	float drop = auto_drops ? auto_drop_at(slot, fract(TIME / drop_interval)) : 0.0;
	float bad = clamp(max(max(drop, forced_drop), 1.0 - signal_quality), 0.0, 1.0);

	// ── 2. Макроблоки: частка зламаних зростає разом з bad ──
	vec2 mb = floor(frag / macroblock_size);
	float mb_hit = step(hash(mb + slot * 1.37), bad);
	float level = mb_hit * bad * (0.5 + 0.5 * hash(mb + slot + 11.0));
	// розмір блоку кроками 1, 2, 4, 8, 16... як у кодеку
	float block = exp2(floor(level * log2(max_block_size) + 0.5));
	vec2 shift = (vec2(hash(mb + slot + 5.0), hash(mb + slot + 9.0)) - 0.5) * block_shift * mb_hit * bad;

	// ── 3. Тремтіння і розриви рядків ──
	float row = floor(frag.y / 2.0);
	float jitter = (hash(vec2(row, fast)) - 0.5) * line_jitter;
	float tear = step(0.985 - bad * 0.06, hash(vec2(floor(frag.y / 6.0), fast)));
	jitter += tear * (hash(vec2(row, fast + 1.0)) - 0.5) * 60.0 * max(bad, 0.15);

	// ── 4. Вибірка: блок береться з mip-рівня, тобто усереднений колір ──
	vec2 p = frag + vec2(jitter, 0.0) + shift;
	vec2 cell = (floor(p / block) + 0.5) * block;
	vec2 uv = clamp(cell * px, vec2(0.0), vec2(1.0));
	float lod = log2(block);
	vec3 src = textureLod(screen_texture, uv, lod).rgb;
	if (color_mode) {
		// хроматичне розшарування, сильніше при поганому сигналі
		vec2 ca = vec2(chroma_shift * (0.3 + bad) * px.x, 0.0);
		src.r = textureLod(screen_texture, clamp(uv + ca, vec2(0.0), vec2(1.0)), lod).r;
		src.b = textureLod(screen_texture, clamp(uv - ca, vec2(0.0), vec2(1.0)), lod).b;
	} else {
		src = vec3(dot(src, vec3(0.299, 0.587, 0.114)));
	}

	vec3 l = (src - 0.5) * contrast + 0.5 + brightness;

	// бендинг: менше відтінків при поганому сигналі
	float levels = mix(256.0, min_gray_levels, bad);
	l = floor(l * levels + 0.5) / levels;

	// темні межі великих блоків
	vec2 in_block = fract(p / block);
	float edges = max(step(in_block.x, 1.0 / block), step(in_block.y, 1.0 / block));
	l -= edges * step(4.0, block) * 0.08;

	// ── 5. Шуми ──
	float n = hash(frag + vec2(fract(TIME * 7.13) * 431.0, fract(TIME * 3.71) * 217.0));
	l += (n - 0.5) * (grain_amount + bad * 0.2);
	if (color_mode) {
		// кольоровий шум, як у аналоговому сигналі
		vec3 cn = vec3(hash(frag + fast * 1.1), hash(frag + fast * 2.3), hash(frag + fast * 3.7)) - 0.5;
		l += cn * bad * 0.35;
	}

	// сніг: цілі рядки білого шуму
	float snow = step(1.0 - bad * 0.08, hash(vec2(row, fast + 3.0)));
	l = mix(l, vec3(hash(frag + fast)), snow * 0.8);

	// смуга перешкод, що повзе вниз
	float d = abs(SCREEN_UV.y - fract(TIME * band_speed));
	d = min(d, 1.0 - d);
	float band = smoothstep(0.06, 0.0, d) * band_strength;
	l += band * 0.12 + (hash(frag * 0.5 + fast) - 0.5) * band;

	// сканлайни і мерехтіння (сильніше при просіданні)
	l -= (0.5 + 0.5 * sin(frag.y * 3.14159 * scanline_density)) * scanline_strength;
	l *= 1.0 + (hash(vec2(fast, 1.0)) - 0.5) * (flicker_amount + bad * 0.15);

	// віньєтка
	vec2 v = SCREEN_UV - 0.5;
	l *= clamp(1.0 - dot(v, v) * vignette_strength * 1.6, 0.0, 1.0);

	vec3 col = clamp(l, 0.0, 1.0) * tint.rgb;

	// ── 6. Приціл ──
	vec2 c = abs(frag - 0.5 / px);
	float hline = step(c.y, 1.0) * step(reticle_gap, c.x) * step(c.x, reticle_size);
	float vline = step(c.x, 1.0) * step(reticle_gap, c.y) * step(c.y, reticle_size);
	col = mix(col, vec3(1.0), clamp(hline + vline, 0.0, 1.0) * reticle_alpha);

	COLOR = vec4(col, 1.0);
}
