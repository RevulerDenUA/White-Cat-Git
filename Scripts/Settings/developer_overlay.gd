extends CanvasLayer

var _label: Label
var _elapsed := 0.0

func _ready() -> void:
	layer = 100
	process_mode = Node.PROCESS_MODE_ALWAYS
	_label = Label.new()
	_label.position = Vector2(18, 12)
	_label.add_theme_font_override("font", WhiteCatTheme.FONT)
	_label.add_theme_font_size_override("font_size", 20)
	_label.add_theme_color_override("font_color", WhiteCatTheme.ACCENT)
	_label.add_theme_color_override("font_shadow_color", Color.BLACK)
	_label.add_theme_constant_override("shadow_outline_size", 6)
	_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_label)
	UserSettings.changed.connect(_update_visible)
	_update_visible()

func _update_visible() -> void:
	_label.visible = UserSettings.values["developer_mode"] and UserSettings.values["show_fps"]

func _process(delta: float) -> void:
	_elapsed += delta
	if _elapsed >= 0.5 and _label.visible:
		_elapsed = 0
		_label.text = "%d FPS" % Engine.get_frames_per_second()
