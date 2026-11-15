extends Node3D

var backdrop: MenuBackdrop
var ui: Control
var menu: MarginContainer
var settings: SettingsPanel
var start_button: MenuAction
var settings_button: MenuAction
var _scrim: ShaderMaterial
var _black: ColorRect
var _loading: Label
var _launching := false
var _dialog_open := false

func _ready() -> void:
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	backdrop = MenuBackdrop.new()
	add_child(backdrop)
	var layer := CanvasLayer.new()
	add_child(layer)
	ui = Control.new()
	ui.theme = WhiteCatTheme.create()
	layer.add_child(ui)
	ui.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	ui.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var shade := ColorRect.new()
	shade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_scrim = ShaderMaterial.new()
	_scrim.shader = load("res://shader/menu_scrim.gdshader")
	shade.material = _scrim
	ui.add_child(shade)
	shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_build_menu()
	_black = ColorRect.new()
	_black.color = Color("070b10")
	_black.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_black.modulate.a = 1
	ui.add_child(_black)
	_black.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_loading = WhiteCatTheme.label("Завантаження…", 23, WhiteCatTheme.MUTED)
	_loading.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_loading.visible = false
	_black.add_child(_loading)
	_loading.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	_loading.position -= _loading.size / 2
	MenuAudio.enter_menu()
	var tween := create_tween()
	tween.tween_property(_black, "modulate:a", 0.0, 0.2 if UserSettings.values["reduced_motion"] else float(backdrop.config.get_value("motion", "intro_seconds", 1.5)))
	start_button.grab_focus()

func _build_menu() -> void:
	menu = MarginContainer.new()
	ui.add_child(menu)
	menu.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for pair: Array in [["left", 112], ["right", 96], ["top", 74], ["bottom", 48]]:
		menu.add_theme_constant_override("margin_" + pair[0], pair[1])
	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 0)
	menu.add_child(column)
	var title := WhiteCatTheme.label("WHITE CAT", 104)
	title.add_theme_font_override("font", WhiteCatTheme.DISPLAY)
	column.add_child(title)
	column.add_child(WhiteCatTheme.spacer(0, true))
	var actions := VBoxContainer.new()
	actions.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	actions.add_theme_constant_override("separation", 14)
	column.add_child(actions)
	start_button = MenuAction.new()
	start_button.name = "StartButton"
	start_button.text = "Розпочати"
	start_button.primary = true
	start_button.pressed.connect(start_game)
	actions.add_child(start_button)
	settings_button = MenuAction.new()
	settings_button.name = "SettingsButton"
	settings_button.text = "Налаштування"
	settings_button.pressed.connect(open_settings)
	var secondary := HBoxContainer.new()
	secondary.add_theme_constant_override("separation", 12)
	actions.add_child(secondary)
	settings_button.symbol = "settings"
	secondary.add_child(settings_button)
	var quit_button := MenuAction.new()
	quit_button.name = "QuitButton"
	quit_button.text = "Вихід"
	quit_button.pressed.connect(_confirm_quit)
	quit_button.symbol = "quit"
	secondary.add_child(quit_button)
	column.add_child(WhiteCatTheme.spacer(64))
	var footer := HBoxContainer.new()
	column.add_child(footer)
	var event_label := WhiteCatTheme.label("NASA Space Apps Challenge 2026", 18, WhiteCatTheme.MUTED)
	event_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	footer.add_child(event_label)
	var credit := WhiteCatTheme.label("Земля: NASA Blue Marble\nХудожній масштаб", 17, WhiteCatTheme.MUTED)
	credit.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	credit.add_theme_color_override("font_shadow_color", Color.BLACK)
	credit.add_theme_constant_override("shadow_outline_size", 4)
	footer.add_child(credit)

func open_settings() -> void:
	if _launching or is_instance_valid(settings):
		return
	menu.hide()
	menu.focus_behavior_recursive = Control.FOCUS_BEHAVIOR_DISABLED
	_scrim.set_shader_parameter("settings_amount", 1.0)
	settings = SettingsPanel.new()
	settings.name = "SettingsPanel"
	ui.add_child(settings)
	settings.closed.connect(func() -> void:
		menu.show()
		menu.focus_behavior_recursive = Control.FOCUS_BEHAVIOR_INHERITED
		_scrim.set_shader_parameter("settings_amount", 0.0)
		settings_button.grab_focus()
	)

func start_game() -> void:
	if _launching:
		return
	var target: String = backdrop.config.get_value("scene", "game_scene", "")
	if target.is_empty() or not ResourceLoader.exists(target, "PackedScene"):
		push_error("Game scene not found: %s" % target)
		MenuDialog.open(ui, "Не вдалося відкрити гру", "Ігрову сцену не знайдено. Перевірте цілісність файлів гри.", ["Гаразд"], 0, menu)
		return
	var request := ResourceLoader.load_threaded_request(target, "PackedScene")
	if request != OK:
		push_error("Game scene request failed: %s, %s" % [target, error_string(request)])
		MenuDialog.open(ui, "Не вдалося відкрити гру", "Не вдалося прочитати файли гри. Спробуйте ще раз.", ["Гаразд"], 0, menu)
		return
	_launching = true
	menu.focus_behavior_recursive = Control.FOCUS_BEHAVIOR_DISABLED
	menu.mouse_behavior_recursive = Control.MOUSE_BEHAVIOR_DISABLED
	var reduced: bool = UserSettings.values["reduced_motion"]
	var seconds := 0.3 if reduced else float(backdrop.config.get_value("motion", "launch_seconds", 3.6))
	MenuAudio.leave_menu(seconds)
	var ui_tween := create_tween()
	ui_tween.tween_property(menu, "modulate:a", 0.0, minf(seconds, 0.45))
	if not reduced:
		MenuAudio.play_transition()
		backdrop.launch(seconds)
	var transition := create_tween()
	transition.tween_interval(maxf(seconds - 0.4, 0.0))
	transition.tween_property(_black, "modulate:a", 1.0, minf(seconds, 0.4))
	await transition.finished
	while ResourceLoader.load_threaded_get_status(target) == ResourceLoader.THREAD_LOAD_IN_PROGRESS:
		_loading.show()
		await get_tree().process_frame
	var status := ResourceLoader.load_threaded_get_status(target)
	if status == ResourceLoader.THREAD_LOAD_LOADED:
		var scene := ResourceLoader.load_threaded_get(target) as PackedScene
		var result := get_tree().change_scene_to_packed(scene)
		if result == OK:
			return
	_launching = false
	_loading.hide()
	_black.modulate.a = 0
	menu.modulate.a = 1
	menu.focus_behavior_recursive = Control.FOCUS_BEHAVIOR_INHERITED
	menu.mouse_behavior_recursive = Control.MOUSE_BEHAVIOR_INHERITED
	backdrop.reset_camera()
	MenuAudio.enter_menu()
	MenuDialog.open(ui, "Не вдалося завантажити гру", "Спробуйте ще раз. Якщо помилка повториться, перевірте файли гри.", ["Гаразд"], 0, menu)

func _confirm_quit() -> void:
	if _dialog_open or _launching:
		return
	_dialog_open = true
	var dialog := MenuDialog.open(ui, "Вийти з гри?", "", ["Вийти", "Залишитися"], 1, menu)
	var choice: int = await dialog.answer
	_dialog_open = false
	if choice == 0:
		get_tree().quit()

func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_cancel") and not is_instance_valid(settings) and not _dialog_open:
		get_viewport().set_input_as_handled()
		_confirm_quit()
