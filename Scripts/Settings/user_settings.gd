extends Node
## Settings are a separate user file; they do not constitute a gameplay save.
signal changed
signal resource_provider_changed

const PATH := "user://settings.cfg"
const DEFAULTS := {
	"master_volume": 0.85, "music_volume": 0.55, "sfx_volume": 0.8, "ui_volume": 0.55,
	"window_mode": 0, "resolution": Vector2i(1600, 900), "vsync": true, "fps_limit": 60,
	"antialiasing": 2, "render_scale": 1.0, "glow": true, "shadows": true,
	"reduced_motion": false, "signal_effects": true, "mouse_sensitivity": 1.0,
	"invert_y": false, "developer_mode": false, "show_fps": false,
	"infinite_resources": false,
}
const ACTIONS := [
	["forwardy", "Рух уперед", "Техніка", KEY_W],
	["backwardy", "Рух назад", "Техніка", KEY_S],
	["lefty", "Поворот ліворуч", "Техніка", KEY_A],
	["righty", "Поворот праворуч", "Техніка", KEY_D],
	["spacy", "Підйом апарата", "Техніка", KEY_SPACE],
	["flight_descend", "Зниження апарата", "Техніка", KEY_SHIFT],
	["CameraLeft", "Огляд ліворуч", "Техніка", KEY_LEFT],
	["CameraRight", "Огляд праворуч", "Техніка", KEY_RIGHT],
	["DrillPlace", "Схема бура / скасувати", "Техніка", KEY_B],
	["DrillConfirm", "Установити бур", "Техніка", KEY_ENTER],
	["interact", "Зібрати ресурси", "Техніка", KEY_G],
	["DrillPickup", "Розібрати бур (утримувати)", "Техніка", KEY_X],
	["CameraChange", "Наступна камера", "Загальні", KEY_C],
	["SpectatorMode", "Вільна камера (розробка)", "Загальні", KEY_F],
	["spectator_forward", "Рух уперед", "Вільна камера", KEY_W],
	["spectator_backward", "Рух назад", "Вільна камера", KEY_S],
	["spectator_left", "Ліворуч", "Вільна камера", KEY_A],
	["spectator_right", "Праворуч", "Вільна камера", KEY_D],
	["spectator_up", "Угору", "Вільна камера", KEY_E],
	["spectator_down", "Униз", "Вільна камера", KEY_Q],
]
var values: Dictionary = DEFAULTS.duplicate(true)
var bindings: Dictionary = default_bindings()
var resource_provider: Node

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	_ensure_buses()
	var file := ConfigFile.new()
	var saved_bindings := {}
	if file.load(PATH) == OK:
		for key: String in DEFAULTS:
			var stored: Variant = file.get_value("settings", key, DEFAULTS[key])
			if typeof(stored) == typeof(DEFAULTS[key]):
				values[key] = stored
		for action: String in bindings:
			var code := int(file.get_value("bindings", action, bindings[action]))
			if code > 0 and code != KEY_ESCAPE and code < (1 << 25):
				bindings[action] = code
				if file.has_section_key("bindings", action):
					saved_bindings[action] = true
	values = validated(values)
	_preserve_saved_bindings(saved_bindings)
	_normalize_bindings()
	apply_all()

static func default_bindings() -> Dictionary:
	var result := {}
	for action: Array in ACTIONS:
		result[action[0]] = action[3]
	return result

static func validated(input: Dictionary) -> Dictionary:
	var result := DEFAULTS.duplicate(true)
	for key: String in DEFAULTS:
		if input.has(key) and typeof(input[key]) == typeof(DEFAULTS[key]):
			result[key] = input[key]
	for key: String in ["master_volume", "music_volume", "sfx_volume", "ui_volume"]:
		result[key] = clampf(result[key], 0.0, 1.0) if is_finite(result[key]) else DEFAULTS[key]
	result["window_mode"] = clampi(result["window_mode"], 0, 2)
	result["antialiasing"] = clampi(result["antialiasing"], 0, 3)
	result["render_scale"] = clampf(result["render_scale"], 0.5, 1.0) if is_finite(result["render_scale"]) else 1.0
	result["mouse_sensitivity"] = clampf(result["mouse_sensitivity"], 0.2, 3.0) if is_finite(result["mouse_sensitivity"]) else 1.0
	if not result["fps_limit"] in [0, 30, 60, 120, 144, 165, 240]:
		result["fps_limit"] = 60
	var resolution: Vector2i = result["resolution"]
	result["resolution"] = Vector2i(clampi(resolution.x, 960, 7680), clampi(resolution.y, 540, 4320))
	return result

func _preserve_saved_bindings(saved: Dictionary) -> void:
	# A newly added default must not erase an existing player's custom shortcut.
	for row: Array in ACTIONS:
		var action: String = row[0]
		if saved.has(action) or binding_conflict(action, bindings[action], bindings).is_empty():
			continue
		for code: int in [row[3], KEY_1, KEY_2, KEY_3, KEY_4, KEY_5, KEY_6, KEY_7, KEY_8, KEY_9, KEY_0, KEY_F1, KEY_F2, KEY_F3, KEY_F4, KEY_F5, KEY_F6, KEY_F7, KEY_F8, KEY_F9, KEY_F10, KEY_F11, KEY_F12]:
			if binding_conflict(action, code, bindings).is_empty():
				bindings[action] = code
				break

func _normalize_bindings() -> void:
	for row: Array in ACTIONS:
		var conflict := binding_conflict(row[0], bindings[row[0]], bindings)
		if not conflict.is_empty():
			bindings = default_bindings()
			push_warning("Conflicting saved bindings; restored defaults.")
			return

static func binding_conflict(action: String, code: int, map: Dictionary) -> String:
	var context := ""
	for row: Array in ACTIONS:
		if row[0] == action:
			context = row[2]
	for row: Array in ACTIONS:
		# Global actions also conflict with either vehicle or spectator movement.
		if row[0] != action and int(map.get(row[0], 0)) == code:
			if row[2] == context or row[2] == "Загальні" or context == "Загальні":
				return row[0]
	return ""

func save_values(next: Dictionary, next_bindings: Dictionary) -> Error:
	var file := ConfigFile.new()
	var clean := validated(next)
	for row: Array in ACTIONS:
		var action: String = row[0]
		if not next_bindings.has(action) or typeof(next_bindings[action]) != TYPE_INT:
			return ERR_INVALID_DATA
		var code: int = next_bindings[action]
		if code <= 0 or code >= (1 << 25) or code == KEY_ESCAPE or not binding_conflict(action, code, next_bindings).is_empty():
			return ERR_INVALID_DATA
	for key: String in clean:
		file.set_value("settings", key, clean[key])
	for action: String in default_bindings():
		file.set_value("bindings", action, int(next_bindings[action]))
	var err := file.save(PATH + ".tmp")
	if err != OK:
		return err
	err = DirAccess.rename_absolute(PATH + ".tmp", PATH)
	if err != OK:
		return err
	values = clean
	bindings = next_bindings.duplicate(true)
	apply_all()
	changed.emit()
	return OK

func apply_all() -> void:
	apply_audio(values)
	apply_display(values)
	apply_render(values)
	apply_bindings()
	_update_resource_provider()

func apply_audio(data: Dictionary) -> void:
	for pair: Array in [["Master", "master_volume"], ["Music", "music_volume"], ["SFX", "sfx_volume"], ["UI", "ui_volume"]]:
		var index := AudioServer.get_bus_index(pair[0])
		AudioServer.set_bus_volume_linear(index, float(data[pair[1]]))

func apply_display(data: Dictionary) -> void:
	Engine.max_fps = int(data["fps_limit"])
	if DisplayServer.get_name() == "headless":
		return
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_ENABLED if data["vsync"] else DisplayServer.VSYNC_DISABLED)
	var mode: int = data["window_mode"]
	var native_mode := DisplayServer.window_get_mode()
	var borderless := DisplayServer.window_get_flag(DisplayServer.WINDOW_FLAG_BORDERLESS)
	if mode == 2:
		if native_mode != DisplayServer.WINDOW_MODE_FULLSCREEN:
			DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_FULLSCREEN)
	elif mode == 1:
		if native_mode != DisplayServer.WINDOW_MODE_WINDOWED or not borderless:
			DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
			DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_BORDERLESS, true)
			var screen := DisplayServer.window_get_current_screen()
			DisplayServer.window_set_size(DisplayServer.screen_get_size(screen))
			DisplayServer.window_set_position(DisplayServer.screen_get_position(screen))
	else:
		var usable := DisplayServer.screen_get_usable_rect()
		var desired: Vector2i = data["resolution"]
		desired = desired.min(usable.size)
		if native_mode != DisplayServer.WINDOW_MODE_WINDOWED or borderless or DisplayServer.window_get_size() != desired:
			DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
			DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_BORDERLESS, false)
			DisplayServer.window_set_size(desired)
			DisplayServer.window_set_position(usable.position + (usable.size - desired) / 2)

func apply_render(data: Dictionary) -> void:
	var vp := get_viewport()
	vp.scaling_3d_scale = float(data["render_scale"])
	vp.screen_space_aa = Viewport.SCREEN_SPACE_AA_FXAA if data["antialiasing"] == 1 else Viewport.SCREEN_SPACE_AA_DISABLED
	vp.msaa_3d = [Viewport.MSAA_DISABLED, Viewport.MSAA_DISABLED, Viewport.MSAA_2X, Viewport.MSAA_4X][int(data["antialiasing"])]

func apply_bindings() -> void:
	for action: String in bindings:
		if not InputMap.has_action(action):
			InputMap.add_action(action)
		# Preserve mouse and controller events supplied by the game.
		for event: InputEvent in InputMap.action_get_events(action):
			if event is InputEventKey:
				InputMap.action_erase_event(action, event)
		var key := InputEventKey.new()
		key.physical_keycode = int(bindings[action])
		InputMap.action_add_event(action, key)

func get_value(key: String) -> Variant:
	return values.get(key, DEFAULTS.get(key))

func binding_text(action: StringName) -> String:
	var labels := PackedStringArray()
	for event: InputEvent in InputMap.action_get_events(action):
		if event is InputEventKey:
			var code: int = event.physical_keycode if event.physical_keycode != 0 else event.keycode
			var label := OS.get_keycode_string(code)
			labels.append("Пробіл" if code == KEY_SPACE else label)
		else:
			labels.append(event.as_text())
	return " / ".join(labels) if not labels.is_empty() else "Не призначено"

func register_resource_provider(provider: Node) -> bool:
	if not is_instance_valid(provider) or not provider.has_method("set_debug_infinite_resources"):
		return false
	if is_instance_valid(resource_provider) and resource_provider.tree_exiting.is_connected(_provider_exiting):
		resource_provider.tree_exiting.disconnect(_provider_exiting)
	resource_provider = provider
	_update_resource_provider()
	provider.tree_exiting.connect(_provider_exiting, CONNECT_ONE_SHOT)
	resource_provider_changed.emit()
	return true

func _provider_exiting() -> void:
	resource_provider = null
	resource_provider_changed.emit()

func _update_resource_provider() -> void:
	if is_instance_valid(resource_provider):
		resource_provider.call("set_debug_infinite_resources", values["developer_mode"] and values["infinite_resources"])

func _ensure_buses() -> void:
	for bus: String in ["Music", "SFX", "UI"]:
		if AudioServer.get_bus_index(bus) == -1:
			AudioServer.add_bus()
			AudioServer.set_bus_name(AudioServer.bus_count - 1, bus)
	var objects := AudioServer.get_bus_index("Object sounds")
	if objects >= 0:
		AudioServer.set_bus_send(objects, "SFX")
