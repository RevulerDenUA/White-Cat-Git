class_name CommandTerminal
extends Control
## Presentation only. The controller parses addresses and owns every control transition.

signal command_submitted(command: String)
const HISTORY_LIMIT := 160
var entry: LineEdit
var output: RichTextLabel
var _lines: Array[String] = []
var _panel: PanelContainer
var _animation: Tween

func _ready() -> void:
	theme = WhiteCatTheme.create()
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var shade := ColorRect.new()
	shade.color = Color(0.025, 0.045, 0.065, 0.87)
	shade.mouse_filter = Control.MOUSE_FILTER_STOP
	add_child(shade)
	shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var margin := MarginContainer.new()
	add_child(margin)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for side: String in ["left", "right", "top", "bottom"]:
		margin.add_theme_constant_override("margin_" + side, 64)
	_panel = PanelContainer.new()
	_panel.add_theme_stylebox_override("panel", WhiteCatTheme.style(Color("101923"), Color("526978"), 1, 28))
	margin.add_child(_panel)
	var body := VBoxContainer.new()
	body.add_theme_constant_override("separation", 20)
	_panel.add_child(body)
	var heading := HBoxContainer.new()
	body.add_child(heading)
	var title := WhiteCatTheme.label("Командний термінал", 30)
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	heading.add_child(title)
	heading.add_child(WhiteCatTheme.label("Esc — до кімнати", 20, WhiteCatTheme.MUTED))
	body.add_child(WhiteCatTheme.rule())
	output = RichTextLabel.new()
	output.name = "Output"
	output.bbcode_enabled = false
	output.selection_enabled = true
	output.scroll_following = true
	output.size_flags_vertical = Control.SIZE_EXPAND_FILL
	output.add_theme_font_size_override("normal_font_size", 23)
	body.add_child(output)
	body.add_child(WhiteCatTheme.rule())
	var command_line := HBoxContainer.new()
	command_line.add_theme_constant_override("separation", 14)
	body.add_child(command_line)
	command_line.add_child(WhiteCatTheme.label(">", 28, WhiteCatTheme.ACCENT))
	entry = LineEdit.new()
	entry.name = "CommandLine"
	entry.max_length = 128
	entry.placeholder_text = "help"
	entry.custom_minimum_size.y = 56
	entry.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	entry.add_theme_stylebox_override("normal", WhiteCatTheme.style(Color("172733")))
	entry.add_theme_stylebox_override("focus", WhiteCatTheme.style(Color("172733"), WhiteCatTheme.ACCENT, 1))
	entry.text_submitted.connect(func(command: String) -> void:
		entry.clear()
		command_submitted.emit(command)
	)
	command_line.add_child(entry)
	hide()

func open() -> void:
	show()
	entry.grab_focus()
	if _animation != null:
		_animation.kill()
	_panel.modulate.a = 0.0
	_animation = create_tween()
	_animation.tween_property(_panel, "modulate:a", 1.0, 0.0 if UserSettings.get_value("reduced_motion") else 0.12)

func close() -> void:
	entry.release_focus()
	entry.clear()
	hide()

func write_line(line: String) -> void:
	for part: String in line.split("\n"):
		_lines.append(part)
	while _lines.size() > HISTORY_LIMIT:
		_lines.pop_front()
	output.text = "\n".join(_lines)
