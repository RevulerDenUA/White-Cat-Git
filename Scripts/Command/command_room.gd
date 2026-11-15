class_name CommandRoomView
extends Node3D
## Wraps the authored model. Geometry, furniture and camera pose remain in the GLB.

@export var controller_path: NodePath
@export var display_size := Vector2i(1024, 401)
@export var imported_light_scale := 0.003
@export var imported_light_range := 30.0
var view_camera: Camera3D
var monitor: MeshInstance3D
var terminal: CommandTerminal
var controller: Node
var hovered := false
var _display_material: StandardMaterial3D
var _hint: Label
var _identity: Label
var _controls: Label
var _pointer := Vector2(-1, -1)
var _hover_animation: Tween
var _pick_body: StaticBody3D
var _monitor_viewport: SubViewport
var _surface := -1
var _last_address := ""
var _identity_animation: Tween
var _vehicle_images: HBoxContainer
var _portraits: Array[TextureRect] = []
var _context := "room"

func _ready() -> void:
	controller = get_node(controller_path)
	var layer_mask: int = controller.get_room_layer()
	for node: Node in $Interior.find_children("*", "", true, false):
		var extras: Dictionary = node.get_meta("extras", {})
		if node is MeshInstance3D:
			node.layers = layer_mask
			if extras.get("ui_target", "") == "main_control":
				monitor = node
		elif node is Camera3D:
			node.current = false
			if extras.get("view_role", "") == "fixed_player_view":
				view_camera = node
		if extras.has("room_light_watts"):
			var light := OmniLight3D.new()
			var color: Array = extras["room_light_color"]
			light.light_color = Color(float(color[0]), float(color[1]), float(color[2]))
			light.light_energy = float(extras["room_light_watts"]) * imported_light_scale
			# Point lights approximate source area lights; suppress their false pin reflections.
			light.light_specular = 0.0
			light.omni_range = imported_light_range
			light.light_cull_mask = layer_mask
			light.layers = layer_mask
			light.shadow_enabled = float(extras["room_light_watts"]) >= 1800.0
			node.add_child(light)
	if view_camera == null or monitor == null:
		push_error("Command room: authored camera or main_control screen metadata is missing.")
		return
	view_camera.cull_mask = layer_mask
	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color("080e17")
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color("b8cbd9")
	environment.ambient_light_energy = 0.45
	view_camera.environment = environment
	for index: int in monitor.mesh.get_surface_count():
		var material := monitor.get_active_material(index)
		if material != null and "screen_off" in material.resource_name:
			_surface = index
	if _surface < 0:
		push_error("Command room: screen_off material surface is missing.")
		return
	_build_monitor(layer_mask)
	_build_ui()
	controller.SessionChanged.connect(_sync_session)
	controller.RegistryChanged.connect(_registry_changed)
	controller.Diagnostic.connect(_diagnostic)
	UserSettings.changed.connect(_sync_session)
	_sync_session.call_deferred()

func get_room_camera() -> Camera3D:
	return view_camera

func _build_monitor(layer_mask: int) -> void:
	_monitor_viewport = SubViewport.new()
	_monitor_viewport.name = "MonitorText"
	_monitor_viewport.size = display_size
	_monitor_viewport.transparent_bg = false
	_monitor_viewport.disable_3d = true
	_monitor_viewport.render_target_update_mode = SubViewport.UPDATE_ONCE
	add_child(_monitor_viewport)
	var background := ColorRect.new()
	background.color = Color("101923")
	_monitor_viewport.add_child(background)
	background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var text := WhiteCatTheme.label("> help", 92, Color("c6dae9"))
	text.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	text.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_monitor_viewport.add_child(text)
	text.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_display_material = StandardMaterial3D.new()
	_display_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_display_material.albedo_texture = _monitor_viewport.get_texture()
	_display_material.albedo_color = Color("a4b0b9")
	monitor.set_surface_override_material(_surface, _display_material)
	var arrays := monitor.mesh.surface_get_arrays(_surface)
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
	var faces := PackedVector3Array()
	var relative := global_transform.affine_inverse() * monitor.global_transform
	if indices.is_empty():
		for vertex: Vector3 in vertices:
			faces.append(relative * vertex)
	else:
		for index: int in indices:
			faces.append(relative * vertices[index])
	var shape := ConcavePolygonShape3D.new()
	shape.backface_collision = false
	shape.set_faces(faces)
	_pick_body = StaticBody3D.new()
	_pick_body.name = "MonitorInteraction"
	_pick_body.collision_layer = layer_mask
	_pick_body.collision_mask = 0
	var collider := CollisionShape3D.new()
	collider.shape = shape
	_pick_body.add_child(collider)
	add_child(_pick_body)

func _build_ui() -> void:
	var layer := CanvasLayer.new()
	layer.name = "TerminalLayer"
	layer.layer = 10
	add_child(layer)
	var ui := Control.new()
	ui.mouse_filter = Control.MOUSE_FILTER_IGNORE
	layer.add_child(ui)
	ui.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_hint = WhiteCatTheme.label("Наведіть на монітор", 22, WhiteCatTheme.ACCENT)
	_hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	ui.add_child(_hint)
	_hint.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	_hint.offset_top = -76
	_hint.offset_bottom = -30
	_identity = WhiteCatTheme.label("", 24)
	_identity.add_theme_color_override("font_shadow_color", Color.BLACK)
	_identity.add_theme_constant_override("shadow_outline_size", 6)
	ui.add_child(_identity)
	_identity.position = Vector2(36, 24)
	_vehicle_images = HBoxContainer.new()
	_vehicle_images.name = "VehicleImages"
	_vehicle_images.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_vehicle_images.add_theme_constant_override("separation", 12)
	ui.add_child(_vehicle_images)
	_vehicle_images.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	_vehicle_images.offset_left = -376
	_vehicle_images.offset_right = -36
	_vehicle_images.offset_top = 24
	_vehicle_images.offset_bottom = 160
	for index: int in 2:
		var frame := PanelContainer.new()
		frame.mouse_filter = Control.MOUSE_FILTER_IGNORE
		frame.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		frame.add_theme_stylebox_override("panel", WhiteCatTheme.style(Color(0.04, 0.07, 0.10, 0.8), Color("394b59"), 1, 10))
		_vehicle_images.add_child(frame)
		var image := TextureRect.new()
		image.name = "VehicleView" + str(index + 1)
		image.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		image.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		image.mouse_filter = Control.MOUSE_FILTER_IGNORE
		image.custom_minimum_size = Vector2(144, 112)
		frame.add_child(image)
		_portraits.append(image)
	_controls = WhiteCatTheme.label("", 20)
	_controls.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_controls.add_theme_color_override("font_shadow_color", Color.BLACK)
	_controls.add_theme_constant_override("shadow_outline_size", 6)
	ui.add_child(_controls)
	_controls.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	_controls.offset_left = 36
	_controls.offset_right = -36
	_controls.offset_top = -100
	_controls.offset_bottom = -24
	terminal = CommandTerminal.new()
	terminal.name = "Terminal"
	ui.add_child(terminal)
	terminal.command_submitted.connect(execute_command)
	terminal.write_line("White Cat\nТестова сцена керування. Доставка апаратів ще не підключена.\nВведіть help, щоб побачити апарати та камери.")

func execute_command(command: String) -> void:
	if controller.snapshot()["context"] != "terminal":
		return
	command = command.strip_edges()
	if command.is_empty():
		return
	terminal.write_line("> " + command)
	var parts := command.split(" ", false)
	if parts.size() == 1 and parts[0].to_lower() == "help":
		_show_help()
	elif parts.size() == 2 and parts[0].to_lower() == "connect":
		var error: String = controller.connect_target(parts[1])
		if not error.is_empty():
			terminal.write_line(error)
	else:
		terminal.write_line("Невідома команда або зайві аргументи. Доступні команди — у help.")

func _show_help() -> void:
	terminal.write_line("help — апарати та камери\nconnect апарат — камера і керування\nconnect апарат/камера — вибір конкретної камери\nEsc — до кімнати\n")
	var targets: Array = controller.catalog()
	if targets.is_empty():
		terminal.write_line("Підключених апаратів немає.")
	for target: Dictionary in targets:
		terminal.write_line("%s — %s" % [target["id"], target["name"]])
		if target["cameras"].is_empty():
			terminal.write_line("  Камери не підключено.")
		else:
			terminal.write_line("  connect " + str(target["id"]))
		for camera: Dictionary in target["cameras"]:
			terminal.write_line("  connect " + str(camera["address"]))

func _registry_changed() -> void:
	if terminal != null and terminal.visible:
		terminal.write_line("Склад апаратів або камер змінився. Оновлений перелік — у help.")

func _diagnostic(message: String) -> void:
	if terminal != null:
		terminal.write_line(message)
	_hint.text = message

func _sync_session() -> void:
	if terminal == null:
		return
	var state: Dictionary = controller.snapshot()
	var context: String = state["context"]
	_context = context
	if context == "terminal":
		if not terminal.visible:
			terminal.open()
	else:
		terminal.close()
	_hint.visible = context == "room"
	_identity.visible = context == "vehicle" or context == "spectator"
	_controls.visible = _identity.visible
	var images: Array = state["identification_images"]
	_vehicle_images.visible = context == "vehicle" and not images.is_empty()
	for index: int in _portraits.size():
		_portraits[index].texture = images[index] if index < images.size() else null
		_portraits[index].get_parent().visible = index < images.size()
	if context != "room":
		_set_hovered(false)
	if context == "vehicle":
		_identity.text = "%s / %s" % [state["target_name"], state["camera_name"]]
		var address: String = state["target_id"] + "/" + state["camera_id"]
		if address != _last_address:
			_last_address = address
			if _identity_animation != null:
				_identity_animation.kill()
			_identity.modulate = WhiteCatTheme.ACCENT
			_identity_animation = create_tween()
			_identity_animation.tween_property(_identity, "modulate", Color.WHITE, 0.0 if UserSettings.get_value("reduced_motion") else 0.18)
		var movement := "%s / %s — рух   %s / %s — поворот" % [UserSettings.binding_text("forwardy"), UserSettings.binding_text("backwardy"), UserSettings.binding_text("lefty"), UserSettings.binding_text("righty")]
		if state["vertical_control"]:
			movement += "   %s / %s — підйом / зниження" % [UserSettings.binding_text("spacy"), UserSettings.binding_text("flight_descend")]
		_controls.text = "%s\n%s — камера   Esc — до кімнати" % [movement, UserSettings.binding_text("CameraChange")]
		if state["look_control"]:
			_controls.text += "   %s / %s — огляд" % [UserSettings.binding_text("CameraLeft"), UserSettings.binding_text("CameraRight")]
		if not state["input_armed"]:
			_controls.text += "   Відпустіть клавіші руху"
	elif context == "spectator":
		_identity.text = "Вільна камера / режим розробника"
		_controls.text = "%s / %s — уперед / назад   %s / %s — ліворуч / праворуч\n%s / %s — угору / вниз   Esc — до кімнати" % [UserSettings.binding_text("spectator_forward"), UserSettings.binding_text("spectator_backward"), UserSettings.binding_text("spectator_left"), UserSettings.binding_text("spectator_right"), UserSettings.binding_text("spectator_up"), UserSettings.binding_text("spectator_down")]
	elif context == "room":
		_hint.text = "Натисніть, щоб відкрити термінал" if hovered else "Наведіть на монітор"

func monitor_at(point: Vector2) -> bool:
	if view_camera == null or not view_camera.current or _pick_body == null:
		return false
	var start := view_camera.project_ray_origin(point)
	var end := start + view_camera.project_ray_normal(point) * view_camera.far
	var query := PhysicsRayQueryParameters3D.create(start, end, int(controller.get_room_layer()))
	var hit := get_world_3d().direct_space_state.intersect_ray(query)
	return not hit.is_empty() and hit["collider"] == _pick_body

func _physics_process(_delta: float) -> void:
	if terminal == null:
		return
	if _context == "room":
		_pointer = get_viewport().get_mouse_position()
		_set_hovered(monitor_at(_pointer))

func _unhandled_input(event: InputEvent) -> void:
	if terminal == null or _context != "room":
		return
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
		if monitor_at(event.position):
			get_viewport().set_input_as_handled()
			controller.open_terminal()

func _set_hovered(value: bool) -> void:
	if hovered == value:
		return
	hovered = value
	if _hint != null:
		_hint.text = "Натисніть, щоб відкрити термінал" if hovered else "Наведіть на монітор"
	if _display_material == null:
		return
	if _hover_animation != null:
		_hover_animation.kill()
	_hover_animation = create_tween()
	_hover_animation.tween_property(_display_material, "albedo_color", Color("eef0ec") if hovered else Color("a4b0b9"), 0.0 if UserSettings.get_value("reduced_motion") else 0.12)
