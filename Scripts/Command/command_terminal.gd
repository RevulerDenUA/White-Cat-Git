class_name CommandTerminal
extends Control
## The same terminal is rendered on the room monitor in standby and interaction.

signal command_submitted(command: String)
signal content_changed
signal display_toggle_requested
const HISTORY_LIMIT := 160
var entry: LineEdit
var output: RichTextLabel
var display_toggle: Button
var _lines: Array[String] = []
var _accepts_commands := false

func _ready() -> void:
	theme = WhiteCatTheme.create()
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var background := ColorRect.new()
	background.color = WhiteCatTheme.PANEL
	background.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(background)
	background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var margin := MarginContainer.new()
	add_child(margin)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for side: String in ["left", "right", "top", "bottom"]:
		margin.add_theme_constant_override("margin_" + side, 22)
	var body := VBoxContainer.new()
	body.add_theme_constant_override("separation", 10)
	margin.add_child(body)
	var heading := HBoxContainer.new()
	body.add_child(heading)
	var title := WhiteCatTheme.label("Командний термінал", 25)
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	heading.add_child(title)
	display_toggle = Button.new()
	display_toggle.focus_mode = Control.FOCUS_NONE
	display_toggle.add_theme_font_size_override("font_size", 19)
	display_toggle.pressed.connect(func() -> void: display_toggle_requested.emit())
	heading.add_child(display_toggle)
	heading.add_child(WhiteCatTheme.label("Esc — до кімнати", 21, WhiteCatTheme.MUTED))
	body.add_child(WhiteCatTheme.rule())
	output = RichTextLabel.new()
	output.name = "Output"
	output.bbcode_enabled = false
	output.selection_enabled = true
	output.scroll_following = true
	output.focus_mode = Control.FOCUS_NONE
	output.size_flags_vertical = Control.SIZE_EXPAND_FILL
	output.add_theme_font_size_override("normal_font_size", 26)
	body.add_child(output)
	var command_line := HBoxContainer.new()
	command_line.add_theme_constant_override("separation", 12)
	body.add_child(command_line)
	command_line.add_child(WhiteCatTheme.label(">", 30, WhiteCatTheme.ACCENT))
	entry = LineEdit.new()
	entry.name = "CommandLine"
	entry.max_length = 128
	entry.keep_editing_on_text_submit = true
	entry.placeholder_text = "help"
	entry.custom_minimum_size.y = 46
	entry.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	entry.add_theme_font_size_override("font_size", 28)
	entry.add_theme_stylebox_override("normal", WhiteCatTheme.style(Color("172733"), Color.TRANSPARENT, 0, 12))
	entry.add_theme_stylebox_override("focus", WhiteCatTheme.style(Color("172733"), WhiteCatTheme.ACCENT, 1, 12))
	entry.focus_exited.connect(_restore_focus.call_deferred)
	entry.text_changed.connect(func(_value: String) -> void: content_changed.emit())
	entry.text_submitted.connect(func(command: String) -> void:
		entry.clear()
		command_submitted.emit(command)
		_restore_focus.call_deferred()
	)
	command_line.add_child(entry)
	standby()

func _restore_focus() -> void:
	if _accepts_commands and is_visible_in_tree():
		entry.grab_focus()
		entry.edit()

func open() -> void:
	_accepts_commands = true
	show()
	entry.editable = true
	_restore_focus()
	_restore_focus.call_deferred()

func standby() -> void:
	_accepts_commands = false
	show()
	entry.editable = false
	entry.release_focus()
	entry.clear()
	content_changed.emit()

func close() -> void:
	_accepts_commands = false
	entry.release_focus()
	entry.clear()
	hide()

func write_line(line: String) -> void:
	for part: String in line.split("\n"):
		_lines.append(part)
	while _lines.size() > HISTORY_LIMIT:
		_lines.pop_front()
	output.text = "\n".join(_lines)
	content_changed.emit()

func _notification(what: int) -> void:
	if what == NOTIFICATION_APPLICATION_FOCUS_IN and is_inside_tree():
		_restore_focus.call_deferred()
