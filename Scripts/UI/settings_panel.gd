class_name SettingsPanel
extends Control
signal closed

const CATEGORIES := ["Звук", "Екран", "Графіка", "Керування", "Зручність", "Розробка", "Автори й джерела"]
const DESCRIPTIONS := [
	"Гучність музики, техніки та інтерфейсу.",
	"Режим вікна, роздільність і частота кадрів.",
	"Якість зображення та навантаження на відеокарту.",
	"Клавіші техніки й вільної камери.",
	"Рух інтерфейсу та зображення з камер.",
	"Інструменти для перевірки гри.",
	"Люди, матеріали й технології, з яких створено White Cat.",
]
var draft: Dictionary
var draft_bindings: Dictionary
var _category := 0
var _frame: MarginContainer
var _layout: BoxContainer
var _nav: BoxContainer
var _content: VBoxContainer
var _scroll: ScrollContainer
var _content_margin: MarginContainer
var _status: Label
var _apply_button: Button
var _tabs: Array[Button] = []
var _waiting := ""
var _key_dialog: MenuDialog
var _capture_button: Button
var _pending_modal := false

func _ready() -> void:
	theme = WhiteCatTheme.create()
	draft = UserSettings.values.duplicate(true)
	draft_bindings = UserSettings.bindings.duplicate(true)
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_build()
	_show_category(0)
	resized.connect(_responsive)
	UserSettings.resource_provider_changed.connect(_provider_changed)
	_responsive()
	_tabs[0].grab_focus()
	var tween := create_tween()
	modulate.a = 0
	tween.tween_property(self, "modulate:a", 1.0, 0.0 if draft["reduced_motion"] else 0.3)

func _build() -> void:
	_frame = MarginContainer.new()
	add_child(_frame)
	_frame.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_frame.add_theme_constant_override("margin_left", 96)
	_frame.add_theme_constant_override("margin_right", 96)
	_frame.add_theme_constant_override("margin_top", 60)
	_frame.add_theme_constant_override("margin_bottom", 50)
	var body := VBoxContainer.new()
	body.add_theme_constant_override("separation", 26)
	_frame.add_child(body)
	var heading := HBoxContainer.new()
	body.add_child(heading)
	var title := WhiteCatTheme.label("Налаштування", 48)
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	heading.add_child(title)
	var back := WhiteCatTheme.button("Назад  ·  Esc")
	back.pressed.connect(request_close)
	heading.add_child(back)
	body.add_child(WhiteCatTheme.rule())
	_layout = BoxContainer.new()
	_layout.add_theme_constant_override("separation", 54)
	_layout.size_flags_vertical = Control.SIZE_EXPAND_FILL
	body.add_child(_layout)
	_nav = BoxContainer.new()
	_nav.vertical = true
	_nav.custom_minimum_size.x = 250
	_nav.add_theme_constant_override("separation", 8)
	_layout.add_child(_nav)
	for i: int in CATEGORIES.size():
		var tab := WhiteCatTheme.button(CATEGORIES[i])
		tab.alignment = HORIZONTAL_ALIGNMENT_LEFT
		tab.custom_minimum_size.y = 60
		tab.toggle_mode = true
		tab.add_theme_stylebox_override("normal", WhiteCatTheme.style(Color.TRANSPARENT))
		var selected := WhiteCatTheme.style(Color("1d2a35"))
		selected.border_color = WhiteCatTheme.ACCENT
		selected.border_width_left = 2
		tab.add_theme_stylebox_override("pressed", selected)
		tab.pressed.connect(_show_category.bind(i))
		_nav.add_child(tab)
		_tabs.append(tab)
	_scroll = ScrollContainer.new()
	_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	_scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_scroll.follow_focus = true
	_layout.add_child(_scroll)
	_content_margin = MarginContainer.new()
	_content_margin.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_content_margin.add_theme_constant_override("margin_right", 22)
	_scroll.add_child(_content_margin)
	_scroll.resized.connect(_limit_content_width)
	_content = VBoxContainer.new()
	_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_content.add_theme_constant_override("separation", 5)
	_content_margin.add_child(_content)
	body.add_child(WhiteCatTheme.rule())
	var footer := HBoxContainer.new()
	footer.add_theme_constant_override("separation", 14)
	body.add_child(footer)
	var reset := WhiteCatTheme.button("За замовчуванням")
	reset.pressed.connect(_reset_defaults)
	footer.add_child(reset)
	_status = WhiteCatTheme.label("", 19, WhiteCatTheme.MUTED)
	_status.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_status.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	footer.add_child(_status)
	var cancel := WhiteCatTheme.button("Скасувати")
	cancel.pressed.connect(_discard_and_close)
	footer.add_child(cancel)
	_apply_button = WhiteCatTheme.button("Застосувати", true)
	_apply_button.custom_minimum_size.x = 180
	_apply_button.pressed.connect(apply_changes)
	footer.add_child(_apply_button)
	_update_dirty()

func _responsive() -> void:
	var narrow := size.x / maxf(size.y, 1) < 1.3
	_layout.vertical = narrow
	_nav.vertical = not narrow
	_nav.custom_minimum_size.x = 0 if narrow else 250
	_layout.add_theme_constant_override("separation", 24 if narrow else 54)
	for tab: Button in _tabs:
		tab.size_flags_horizontal = Control.SIZE_EXPAND_FILL if narrow else Control.SIZE_FILL
	_limit_content_width()

func _limit_content_width() -> void:
	if _content_margin != null:
		var inset := maxi(22, roundi(_scroll.size.x) - 1120)
		if _content_margin.get_theme_constant("margin_right") != inset:
			_content_margin.add_theme_constant_override("margin_right", inset)

func _show_category(index: int) -> void:
	var old_focus := get_viewport().gui_get_focus_owner()
	var restore_focus := old_focus != null and _content.is_ancestor_of(old_focus)
	_category = index
	for child: Node in _content.get_children():
		_content.remove_child(child)
		child.queue_free()
	for i: int in _tabs.size():
		_tabs[i].set_pressed_no_signal(i == index)
		_tabs[i].add_theme_color_override("font_color", WhiteCatTheme.ACCENT if i == index else WhiteCatTheme.MUTED)
	_content.add_child(WhiteCatTheme.label(CATEGORIES[index], 32))
	var description := WhiteCatTheme.label(DESCRIPTIONS[index], 21, WhiteCatTheme.MUTED)
	description.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_content.add_child(description)
	_content.add_child(WhiteCatTheme.spacer(22))
	match index:
		0:
			_slider("Загальна гучність", "master_volume", 0, 1, 0.01, "%")
			_slider("Музика", "music_volume", 0, 1, 0.01, "%")
			_slider("Звуки техніки", "sfx_volume", 0, 1, 0.01, "%")
			_slider("Інтерфейс", "ui_volume", 0, 1, 0.01, "%")
		1:
			_option("Режим екрана", "window_mode", ["У вікні", "Без рамки", "Повний екран"], [0, 1, 2])
			var resolutions: Array = [Vector2i(1280, 720), Vector2i(1600, 900), Vector2i(1920, 1080), Vector2i(2560, 1440), Vector2i(3840, 2160)]
			if not resolutions.has(draft["resolution"]):
				resolutions.append(draft["resolution"])
			var names: Array = []
			for resolution: Vector2i in resolutions:
				names.append("%d × %d" % [resolution.x, resolution.y])
			var resolution_button := _option("Роздільність вікна", "resolution", names, resolutions)
			resolution_button.disabled = draft["window_mode"] != 0
			_note("У повноекранних режимах використовується роздільність монітора. Якість рендерингу змінюється в розділі «Графіка».")
			_toggle("Вертикальна синхронізація", "vsync")
			_option("Обмеження FPS", "fps_limit", ["30", "60", "120", "144", "165", "240", "Без обмеження"], [30, 60, 120, 144, 165, 240, 0])
		2:
			_option("Згладжування", "antialiasing", ["Вимкнено", "FXAA", "MSAA 2×", "MSAA 4×"], [0, 1, 2, 3])
			_slider("Масштаб рендерингу 3D", "render_scale", 0.5, 1, 0.05, "%")
			_note("Зменшує навантаження на відеокарту. Текст і меню залишаються чіткими.")
			_toggle("Тіні станції", "shadows")
			var glow := _toggle("Світіння яскравих ділянок", "glow")
			if RenderingServer.get_current_rendering_method() == "gl_compatibility":
				glow.disabled = true
				_note("Поточний графічний режим не підтримує світіння.")
		3:
			_slider("Чутливість миші", "mouse_sensitivity", 0.2, 3, 0.05, "×")
			_toggle("Інвертувати вертикаль миші", "invert_y")
			_note("Оберіть клавішу праворуч і натисніть нову. Esc скасовує зміну. Однакові клавіші дозволені для різних режимів керування.")
			var group := ""
			for action: Array in UserSettings.ACTIONS:
				if group != action[2]:
					group = action[2]
					_content.add_child(WhiteCatTheme.spacer(20))
					_content.add_child(WhiteCatTheme.label(group, 20, WhiteCatTheme.ACCENT))
				var button := WhiteCatTheme.button(key_name(int(draft_bindings[action[0]])))
				button.custom_minimum_size.x = 220
				button.pressed.connect(_begin_rebind.bind(action[0], action[1], button))
				_row(action[1], button)
		4:
			_toggle("Зменшити рух і переходи", "reduced_motion")
			_note("Зупиняє рух камери в меню й замінює наближення до станції коротким згасанням.")
			_toggle("Ефекти відеосигналу камер", "signal_effects")
			_note("Керує перешкодами, мерехтінням і фільтром у зображенні з камер техніки.")
		5:
			_toggle("Режим розробника", "developer_mode")
			_note("Діагностика та інструменти перевірки. Параметри зберігаються окремо від ігрового прогресу.")
			var fps := _toggle("Показувати FPS", "show_fps")
			fps.disabled = not draft["developer_mode"]
			var resources := _toggle("Безкінечні ресурси", "infinite_resources")
			resources.disabled = not draft["developer_mode"] or not is_instance_valid(UserSettings.resource_provider)
			if not is_instance_valid(UserSettings.resource_provider):
				_note("Системи ресурсів у поточній сцені ще немає. Цей параметр стане доступним після її підключення.")
		6:
			var credits := CreditsView.new()
			credits.license_requested.connect(_open_license)
			_content.add_child(credits)
	_scroll.scroll_vertical = 0
	if restore_focus:
		_tabs[index].grab_focus()

func _open_license(title: String, text: String) -> void:
	if _pending_modal:
		return
	_pending_modal = true
	var dialog := CreditsLicenseDialog.open(self, title, text, _frame)
	dialog.closed.connect(func() -> void: _pending_modal = false)

func _row(title: String, control: Control) -> void:
	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_top", 10)
	margin.add_theme_constant_override("margin_bottom", 10)
	_content.add_child(margin)
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 26)
	margin.add_child(row)
	var label := WhiteCatTheme.label(title, 23)
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	row.add_child(label)
	control.custom_minimum_size.x = maxf(control.custom_minimum_size.x, 310)
	control.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	row.add_child(control)
	_content.add_child(WhiteCatTheme.rule())

func _note(text: String) -> void:
	var note := WhiteCatTheme.label(text, 19, WhiteCatTheme.MUTED)
	note.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	note.custom_minimum_size.x = 780
	note.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	_content.add_child(note)
	_content.add_child(WhiteCatTheme.spacer(12))

func _slider(title: String, key: String, low: float, high: float, step: float, unit: String) -> void:
	var box := HBoxContainer.new()
	box.add_theme_constant_override("separation", 20)
	var slider := HSlider.new()
	slider.min_value = low
	slider.max_value = high
	slider.step = step
	slider.value = float(draft[key])
	slider.custom_minimum_size = Vector2(230, 44)
	box.add_child(slider)
	var value := WhiteCatTheme.label(_format_value(slider.value, unit), 21, WhiteCatTheme.ACCENT)
	value.custom_minimum_size.x = 78
	value.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	box.add_child(value)
	slider.value_changed.connect(func(next: float) -> void:
		draft[key] = next
		value.text = _format_value(next, unit)
		if key.ends_with("volume"):
			UserSettings.apply_audio(draft)
		_update_dirty()
	)
	_row(title, box)

func _format_value(value: float, unit: String) -> String:
	return "%d %%" % roundi(value * 100) if unit == "%" else "%.2f ×" % value

func _toggle(title: String, key: String) -> Button:
	var button := WhiteCatTheme.button("Увімкнено" if draft[key] else "Вимкнено")
	button.toggle_mode = true
	button.button_pressed = draft[key]
	button.toggled.connect(func(on: bool) -> void:
		draft[key] = on
		button.text = "Увімкнено" if on else "Вимкнено"
		_update_dirty()
		if key == "developer_mode":
			_show_category.call_deferred(_category)
	)
	_row(title, button)
	return button

func _option(title: String, key: String, labels: Array, options: Array) -> OptionButton:
	var button := OptionButton.new()
	button.custom_minimum_size.y = 50
	for option: Variant in labels:
		button.add_item(str(option))
	button.selected = maxi(options.find(draft[key]), 0)
	button.item_selected.connect(func(index: int) -> void:
		draft[key] = options[index]
		_update_dirty()
		if key == "window_mode":
			_show_category.call_deferred(_category)
	)
	_row(title, button)
	return button

func _is_dirty() -> bool:
	return draft != UserSettings.values or draft_bindings != UserSettings.bindings

func _update_dirty() -> void:
	if _status == null:
		return
	_status.text = "Зміни не збережено" if _is_dirty() else "Збережено"
	_apply_button.disabled = not _is_dirty()

func apply_changes() -> bool:
	if _pending_modal or not _is_dirty():
		return not _is_dirty()
	var old := UserSettings.values.duplicate(true)
	var display_changed: bool = draft["window_mode"] != old["window_mode"] or draft["resolution"] != old["resolution"]
	if display_changed:
		UserSettings.apply_display(draft)
		_pending_modal = true
		var dialog := MenuDialog.open(self, "Залишити ці налаштування?", "Попередній режим повернеться через 15 с.", ["Залишити", "Повернути"], 1, _frame)
		_display_countdown(dialog)
		var choice: int = await dialog.answer
		_pending_modal = false
		if choice != 0:
			UserSettings.apply_display(old)
			draft["window_mode"] = old["window_mode"]
			draft["resolution"] = old["resolution"]
			_show_category(_category)
			_update_dirty()
			return false
	var result := UserSettings.save_values(draft, draft_bindings)
	if result != OK:
		UserSettings.apply_display(old)
		push_error("Settings file write failed: %s" % error_string(result))
		var error_dialog := MenuDialog.open(self, "Не вдалося зберегти", "Гра не може записати налаштування. Зміни залишилися в цьому вікні; спробуйте ще раз.", ["Гаразд"], 0, _frame)
		await error_dialog.answer
		return false
	draft = UserSettings.values.duplicate(true)
	MenuAudio.play_confirm()
	_update_dirty()
	return true

func _display_countdown(dialog: MenuDialog) -> void:
	for remaining: int in range(15, 0, -1):
		if not is_instance_valid(dialog) or dialog._finished:
			return
		dialog.body_label.text = "Якщо зображення незручне, попередній режим повернеться через %d с." % remaining
		await get_tree().create_timer(1.0).timeout
	if is_instance_valid(dialog) and not dialog._finished:
		dialog.finish(1)

func request_close() -> void:
	if _pending_modal or not _waiting.is_empty():
		return
	if _is_dirty():
		_pending_modal = true
		var dialog := MenuDialog.open(self, "Є незбережені зміни", "Застосувати їх перед поверненням до меню?", ["Застосувати", "Відкинути", "Залишитися"], 2, _frame)
		var choice: int = await dialog.answer
		_pending_modal = false
		if choice == 0:
			if await apply_changes():
				_close()
		elif choice == 1:
			_discard_and_close()
	else:
		_close()

func _discard_and_close() -> void:
	UserSettings.apply_audio(UserSettings.values)
	_close()

func _close() -> void:
	closed.emit()
	queue_free()

func _reset_defaults() -> void:
	if _pending_modal:
		return
	_pending_modal = true
	var dialog := MenuDialog.open(self, "Відновити стандартні параметри?", "Зміняться всі розділи, включно з клавішами. Зміни набудуть чинності після застосування.", ["Відновити", "Скасувати"], 1, _frame)
	var choice: int = await dialog.answer
	_pending_modal = false
	if choice == 0:
		draft = UserSettings.DEFAULTS.duplicate(true)
		draft_bindings = UserSettings.default_bindings()
		UserSettings.apply_audio(draft)
		_show_category(_category)
		_update_dirty()

func _begin_rebind(action: String, title: String, button: Button) -> void:
	_waiting = action
	_capture_button = button
	_key_dialog = MenuDialog.open(self, title, "Натисніть нову клавішу. Esc — скасувати.", ["Скасувати"], 0, _frame)
	_key_dialog.answer.connect(func(_choice: int) -> void: _waiting = "")

func _input(event: InputEvent) -> void:
	if _waiting.is_empty() or not event is InputEventKey or not event.pressed or event.echo:
		return
	get_viewport().set_input_as_handled()
	if event.keycode == KEY_ESCAPE or event.physical_keycode == KEY_ESCAPE:
		_key_dialog.finish(-1)
		return
	var code: int = event.physical_keycode if event.physical_keycode != 0 else event.keycode
	var conflict := UserSettings.binding_conflict(_waiting, code, draft_bindings)
	if not conflict.is_empty():
		var conflict_title := conflict
		for action: Array in UserSettings.ACTIONS:
			if action[0] == conflict:
				conflict_title = action[1]
		_key_dialog.body_label.text = "«%s» уже використовується для дії «%s». Натисніть іншу клавішу." % [key_name(code), conflict_title]
		return
	draft_bindings[_waiting] = code
	_capture_button.text = key_name(code)
	_key_dialog.finish(0)
	_update_dirty()

static func key_name(code: int) -> String:
	var key := OS.get_keycode_string(code)
	return "Пробіл" if code == KEY_SPACE else key

func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_cancel") and not _pending_modal and _waiting.is_empty():
		get_viewport().set_input_as_handled()
		request_close()

func _provider_changed() -> void:
	if _category == 5:
		_show_category(5)
