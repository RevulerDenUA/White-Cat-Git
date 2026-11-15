class_name MenuAction
extends Button

var primary := false
var symbol := "station"
var emphasis := 0.0
var _animation: Tween

func _ready() -> void:
	custom_minimum_size = Vector2(540, 102) if primary else Vector2(264, 76)
	alignment = HORIZONTAL_ALIGNMENT_LEFT
	mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	add_theme_font_size_override("font_size", 34 if primary else 23)
	for state: String in ["font_color", "font_focus_color", "font_hover_color", "font_pressed_color"]:
		add_theme_color_override(state, Color.TRANSPARENT)
	for state: String in ["normal", "hover", "pressed", "focus", "disabled"]:
		var box := StyleBoxEmpty.new()
		box.content_margin_left = 94 if primary else 64
		box.content_margin_right = 22
		add_theme_stylebox_override(state, box)
	mouse_entered.connect(_update_emphasis.bind(true))
	mouse_exited.connect(_update_emphasis.bind(false))
	focus_entered.connect(_update_emphasis.bind(true))
	focus_exited.connect(_update_emphasis.bind(false))
	button_down.connect(func() -> void:
		offset_transform_enabled = true
		offset_transform_position = Vector2(0, 3)
		MenuAudio.play_confirm()
	)
	button_up.connect(func() -> void: offset_transform_position = Vector2.ZERO)

func _update_emphasis(over: bool) -> void:
	if _animation != null:
		_animation.kill()
	var target := 1.0 if over or has_focus() else 0.0
	if over:
		MenuAudio.play_hover()
	_animation = create_tween()
	_animation.tween_property(self, "emphasis", target, 0.0 if UserSettings.values["reduced_motion"] else 0.18)
	_animation.tween_callback(queue_redraw)
	set_process(true)

func _process(_delta: float) -> void:
	queue_redraw()
	if _animation == null or not _animation.is_running():
		set_process(false)

func _shape(offset: Vector2 = Vector2.ZERO) -> PackedVector2Array:
	var cut := 16.0 if primary else 10.0
	return PackedVector2Array([
		Vector2(0, 0) + offset, Vector2(size.x - cut, 0) + offset,
		Vector2(size.x, cut) + offset, Vector2(size.x, size.y) + offset,
		Vector2(cut, size.y) + offset, Vector2(0, size.y - cut) + offset,
	])

func _draw() -> void:
	var body := Color("d4dedf").lerp(Color("edf1ed"), emphasis) if primary else Color("1b2b38").lerp(Color("334b5c"), emphasis)
	draw_colored_polygon(_shape(Vector2(0, 5)), Color("08111a"))
	draw_colored_polygon(_shape(), body)
	var contour := _shape()
	contour.append(contour[0])
	draw_polyline(contour, Color("f2f3ee") if primary else Color("526978"), 1.0, true)
	if has_focus():
		draw_polyline(contour, Color("e1edf4"), 2.0, true)
	var ink := Color("334b58") if primary else Color("c5d4dc")
	_draw_symbol(Vector2(44 if primary else 32, size.y * 0.5), ink)
	if primary:
		draw_line(Vector2(72, 27), Vector2(72, size.y - 27), Color("a9b9bf"), 1, true)
	var font := get_theme_font("font")
	var font_size := get_theme_font_size("font_size")
	var baseline := (size.y + font.get_ascent(font_size) - font.get_descent(font_size)) * 0.5
	var inset := 94.0 if primary else 64.0
	var type_color := Color("182731") if primary else WhiteCatTheme.WHITE
	draw_string(font, Vector2(inset, baseline), text, HORIZONTAL_ALIGNMENT_LEFT, size.x - inset - 22, font_size, type_color)

func _draw_symbol(center: Vector2, ink: Color) -> void:
	if symbol == "station":
		draw_arc(center, 17, 0, TAU, 48, ink, 1.6, true)
		draw_arc(center, 5, 0, TAU, 24, ink, 1.6, true)
		for i: int in 3:
			var direction := Vector2.from_angle(-PI * 0.5 + TAU * float(i) / 3.0)
			draw_line(center + direction * 5, center + direction * 17, ink, 1.6, true)
	elif symbol == "settings":
		draw_arc(center, 10, 0, TAU, 32, ink, 1.5, true)
		draw_arc(center, 3, 0, TAU, 16, ink, 1.5, true)
		for i: int in 8:
			var direction := Vector2.from_angle(TAU * float(i) / 8.0)
			draw_line(center + direction * 10, center + direction * 14, ink, 2.8, true)
	else:
		draw_arc(center, 11, -PI * 0.32, PI * 1.32, 32, ink, 1.8, true)
		draw_line(center + Vector2(0, -14), center + Vector2(0, -1), ink, 1.8, true)
