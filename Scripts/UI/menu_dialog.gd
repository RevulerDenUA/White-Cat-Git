class_name MenuDialog
extends ColorRect
signal answer(index: int)

var body_label: Label
var _under: Control
var _previous: Control
var _finished := false

static func open(parent: Control, title: String, body: String, options: Array[String], safe_index: int = -1, under: Control = null) -> MenuDialog:
	var dialog := MenuDialog.new()
	dialog.color = Color(0.015, 0.025, 0.04, 0.84)
	dialog._under = under
	dialog._previous = parent.get_viewport().gui_get_focus_owner()
	if under != null:
		under.focus_behavior_recursive = Control.FOCUS_BEHAVIOR_DISABLED
		under.mouse_behavior_recursive = Control.MOUSE_BEHAVIOR_DISABLED
	parent.add_child(dialog)
	dialog.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var center := CenterContainer.new()
	dialog.add_child(center)
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var panel := PanelContainer.new()
	panel.custom_minimum_size.x = 680
	panel.add_theme_stylebox_override("panel", WhiteCatTheme.style(WhiteCatTheme.PANEL, WhiteCatTheme.LINE, 1, 36))
	center.add_child(panel)
	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 24)
	panel.add_child(column)
	column.add_child(WhiteCatTheme.spacer(12))
	column.add_child(WhiteCatTheme.label(title, 32))
	dialog.body_label = WhiteCatTheme.label(body, 22, WhiteCatTheme.MUTED)
	dialog.body_label.visible = not body.is_empty()
	dialog.body_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	dialog.body_label.custom_minimum_size.x = 600
	column.add_child(dialog.body_label)
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 12)
	column.add_child(row)
	var buttons: Array[Button] = []
	for i: int in options.size():
		var button := WhiteCatTheme.button(options[i], i == 0 and options.size() > 1)
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		button.pressed.connect(dialog.finish.bind(i))
		row.add_child(button)
		buttons.append(button)
	column.add_child(WhiteCatTheme.spacer(12))
	var focus_index := options.size() - 1 if safe_index < 0 else safe_index
	buttons[focus_index].grab_focus()
	return dialog

func finish(index: int) -> void:
	if _finished:
		return
	_finished = true
	if is_instance_valid(_under):
		_under.focus_behavior_recursive = Control.FOCUS_BEHAVIOR_INHERITED
		_under.mouse_behavior_recursive = Control.MOUSE_BEHAVIOR_INHERITED
	if is_instance_valid(_previous):
		_previous.grab_focus()
	answer.emit(index)
	queue_free()

func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_cancel"):
		get_viewport().set_input_as_handled()
		finish(-1)
