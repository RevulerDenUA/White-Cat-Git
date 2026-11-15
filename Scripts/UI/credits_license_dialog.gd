class_name CreditsLicenseDialog
extends ColorRect

signal closed

var _under: Control
var _previous: Control
var _finished := false
var reader: TextEdit

static func open(parent: Control, title: String, text: String, under: Control) -> CreditsLicenseDialog:
	var dialog := CreditsLicenseDialog.new()
	dialog.color = WhiteCatTheme.INK
	dialog._under = under
	dialog._previous = parent.get_viewport().gui_get_focus_owner()
	under.focus_behavior_recursive = Control.FOCUS_BEHAVIOR_DISABLED
	under.mouse_behavior_recursive = Control.MOUSE_BEHAVIOR_DISABLED
	parent.add_child(dialog)
	dialog.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var frame := MarginContainer.new()
	dialog.add_child(frame)
	frame.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for edge: String in ["left", "right"]:
		frame.add_theme_constant_override("margin_" + edge, 96)
	for edge: String in ["top", "bottom"]:
		frame.add_theme_constant_override("margin_" + edge, 60)
	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 24)
	frame.add_child(column)
	var header := HBoxContainer.new()
	header.add_theme_constant_override("separation", 24)
	column.add_child(header)
	var heading := WhiteCatTheme.label(title, 32)
	heading.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	heading.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(heading)
	var back := WhiteCatTheme.button("Закрити  ·  Esc")
	back.pressed.connect(dialog.finish)
	header.add_child(back)
	column.add_child(WhiteCatTheme.rule())
	dialog.reader = TextEdit.new()
	dialog.reader.name = "LicenseText"
	dialog.reader.text = text
	dialog.reader.editable = false
	dialog.reader.wrap_mode = TextEdit.LINE_WRAPPING_BOUNDARY
	dialog.reader.size_flags_vertical = Control.SIZE_EXPAND_FILL
	dialog.reader.add_theme_font_override("font", WhiteCatTheme.FONT)
	dialog.reader.add_theme_font_size_override("font_size", 20)
	dialog.reader.add_theme_color_override("font_readonly_color", WhiteCatTheme.WHITE)
	dialog.reader.add_theme_stylebox_override("read_only", WhiteCatTheme.style(WhiteCatTheme.PANEL, WhiteCatTheme.LINE, 1, 24))
	column.add_child(dialog.reader)
	column.add_child(WhiteCatTheme.label("Текст можна прокручувати й копіювати.", 19, WhiteCatTheme.MUTED))
	back.grab_focus()
	return dialog

func finish() -> void:
	if _finished:
		return
	_finished = true
	if is_instance_valid(_under):
		_under.focus_behavior_recursive = Control.FOCUS_BEHAVIOR_INHERITED
		_under.mouse_behavior_recursive = Control.MOUSE_BEHAVIOR_INHERITED
	if is_instance_valid(_previous):
		_previous.grab_focus()
	closed.emit()
	queue_free()

func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_cancel"):
		get_viewport().set_input_as_handled()
		finish()
