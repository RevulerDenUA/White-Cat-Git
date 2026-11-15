class_name CommandRoomView
extends Node3D
## One physical screen: the actual terminal or a single shared-world camera feed.

@export var controller_path: NodePath
@export var display_size := Vector2i(1920, 752)
@export var imported_light_scale := 0.003
@export var imported_light_range := 30.0
var view_camera: Camera3D
var monitor: MeshInstance3D
var terminal: CommandTerminal
var controller: Node
var hovered := false
var keyboard: PhysicalCommandKeyboard
var _display_material: StandardMaterial3D
var _hint: Label
var _identity: Label
var _controls: Label
var _hover_animation: Tween
var _pick_body: StaticBody3D
var _monitor_viewport: SubViewport
var _feed_camera: Camera3D
var _source_camera: Camera3D
var _signal_filter: ColorRect
var _surface := -1
var _vehicle_images: HBoxContainer
var _portraits: Array[TextureRect] = []
var _context := "room"
var _housing: MeshInstance3D
var _keyboard_mesh: MeshInstance3D
var _hover_shell: MeshInstance3D
var _overview: Transform3D
var _overview_fov: float
var _pose_animation: Tween
var _close_view := false
var _screen_vertices: PackedVector3Array
var _screen_indices: PackedInt32Array
var _screen_uvs: PackedVector2Array

func _ready() -> void:
	# Copy after source cameras have applied their movement tilt and player look.
	process_priority = 100
	controller = get_node(controller_path)
	var layer_mask: int = controller.get_room_layer()
	for node: Node in $Interior.find_children("*", "", true, false):
		var extras: Dictionary = node.get_meta("extras", {})
		if node is MeshInstance3D:
			node.layers = layer_mask
			if extras.get("ui_target", "") == "main_control":
				monitor = node
			elif node.name == "Terminal | CRT housing":
				_housing = node
			elif node.name == "Terminal | Keyboard":
				_keyboard_mesh = node
		elif node is Camera3D:
			node.current = false
			if extras.get("view_role", "") == "fixed_player_view":
				view_camera = node
		if extras.has("room_light_watts"):
			var light := OmniLight3D.new()
			var color: Array = extras["room_light_color"]
			light.light_color = Color(float(color[0]), float(color[1]), float(color[2]))
			light.light_energy = float(extras["room_light_watts"]) * imported_light_scale
			light.light_specular = 0.0
			light.omni_range = imported_light_range
			light.light_cull_mask = layer_mask
			light.layers = layer_mask
			light.shadow_enabled = float(extras["room_light_watts"]) >= 1800.0
			node.add_child(light)
	if view_camera == null or monitor == null or _housing == null or _keyboard_mesh == null:
		push_error("Command room: authored camera, screen, housing or keyboard is missing.")
		return
	_overview = view_camera.global_transform
	_overview_fov = view_camera.fov
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
	keyboard = PhysicalCommandKeyboard.new()
	_keyboard_mesh.add_child(keyboard)
	keyboard.build(_keyboard_mesh, layer_mask)
	_build_hover_shell(layer_mask)
	controller.SessionChanged.connect(_sync_session)
	controller.RegistryChanged.connect(_registry_changed)
	controller.Diagnostic.connect(_diagnostic)
	UserSettings.changed.connect(_sync_session)
	get_viewport().size_changed.connect(_resize_work_view)
	_sync_session.call_deferred()

func get_room_camera() -> Camera3D:
	return view_camera

func get_display_viewport() -> SubViewport:
	return _monitor_viewport

func _build_monitor(layer_mask: int) -> void:
	_monitor_viewport = SubViewport.new()
	_monitor_viewport.name = "MonitorDisplay"
	_monitor_viewport.size = display_size
	_monitor_viewport.size_2d_override = Vector2i(1280, 502)
	_monitor_viewport.size_2d_override_stretch = true
	_monitor_viewport.world_3d = get_world_3d()
	_monitor_viewport.handle_input_locally = true
	_monitor_viewport.disable_3d = true
	_monitor_viewport.render_target_update_mode = SubViewport.UPDATE_ONCE
	add_child(_monitor_viewport)
	_feed_camera = Camera3D.new()
	_feed_camera.name = "SelectedVehicleFeed"
	_feed_camera.cull_mask = 1
	_monitor_viewport.add_child(_feed_camera)
	_feed_camera.make_current()
	_display_material = StandardMaterial3D.new()
	_display_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_display_material.albedo_texture = _monitor_viewport.get_texture()
	monitor.set_surface_override_material(_surface, _display_material)
	var arrays := monitor.mesh.surface_get_arrays(_surface)
	_screen_vertices = arrays[Mesh.ARRAY_VERTEX]
	_screen_indices = arrays[Mesh.ARRAY_INDEX]
	_screen_uvs = arrays[Mesh.ARRAY_TEX_UV]
	var faces := PackedVector3Array()
	var relative := global_transform.affine_inverse() * monitor.global_transform
	for index: int in _screen_indices:
		faces.append(relative * _screen_vertices[index])
	var shape := ConcavePolygonShape3D.new()
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
	layer.name = "DisplayHud"
	layer.layer = 10
	_monitor_viewport.add_child(layer)
	var ui := Control.new()
	ui.mouse_filter = Control.MOUSE_FILTER_IGNORE
	layer.add_child(ui)
	ui.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_signal_filter = ColorRect.new()
	_signal_filter.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_signal_filter.material = controller.get_signal_material()
	ui.add_child(_signal_filter)
	_signal_filter.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_identity = WhiteCatTheme.label("", 26)
	_identity.clip_text = true
	_identity.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	_identity.add_theme_color_override("font_shadow_color", Color.BLACK)
	_identity.add_theme_constant_override("shadow_outline_size", 6)
	ui.add_child(_identity)
	_identity.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	_identity.offset_left = 24
	_identity.offset_right = -280
	_identity.offset_top = 18
	_identity.offset_bottom = 52
	_vehicle_images = HBoxContainer.new()
	_vehicle_images.name = "VehicleImages"
	_vehicle_images.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_vehicle_images.add_theme_constant_override("separation", 12)
	ui.add_child(_vehicle_images)
	_vehicle_images.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	_vehicle_images.offset_left = -260
	_vehicle_images.offset_right = -24
	_vehicle_images.offset_top = 18
	_vehicle_images.offset_bottom = 112
	for index: int in 2:
		var portrait := TextureRect.new()
		portrait.name = "VehicleView" + str(index + 1)
		portrait.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		portrait.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		portrait.mouse_filter = Control.MOUSE_FILTER_IGNORE
		portrait.custom_minimum_size = Vector2(112, 94)
		portrait.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		_vehicle_images.add_child(portrait)
		_portraits.append(portrait)
	_controls = WhiteCatTheme.label("", 24)
	_controls.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_controls.add_theme_color_override("font_shadow_color", Color.BLACK)
	_controls.add_theme_constant_override("shadow_outline_size", 6)
	ui.add_child(_controls)
	_controls.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	_controls.offset_left = 24
	_controls.offset_right = -24
	_controls.offset_top = -82
	_controls.offset_bottom = -14
	terminal = CommandTerminal.new()
	terminal.name = "Terminal"
	ui.add_child(terminal)
	terminal.command_submitted.connect(execute_command)
	terminal.content_changed.connect(_refresh_standby)
	terminal.write_line("White Cat
Доставка апаратів ще не підключена.
Введіть help, щоб побачити апарати та камери.")
	var root_layer := CanvasLayer.new()
	root_layer.name = "RoomHints"
	add_child(root_layer)
	_hint = WhiteCatTheme.label("Наведіть на монітор", 24, WhiteCatTheme.ACCENT)
	_hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	root_layer.add_child(_hint)
	_hint.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	_hint.offset_top = -70
	_hint.offset_bottom = -24

func _refresh_standby() -> void:
	if _monitor_viewport != null and _context == "room":
		_monitor_viewport.render_target_update_mode = SubViewport.UPDATE_ONCE

func _build_hover_shell(layer_mask: int) -> void:
	_hover_shell = MeshInstance3D.new()
	_hover_shell.name = "MonitorHoverHalo"
	_hover_shell.mesh = _housing.mesh
	_hover_shell.layers = layer_mask
	_hover_shell.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var material := StandardMaterial3D.new()
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.cull_mode = BaseMaterial3D.CULL_FRONT
	material.grow = true
	material.grow_amount = 0.045
	material.albedo_color = Color(0.58, 0.82, 1.0, 0.0)
	_hover_shell.material_override = material
	_housing.add_child(_hover_shell)
	_hover_shell.visible = false

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
	_context = state["context"]
	_source_camera = controller.get_active_camera()
	var piloting := _context == "vehicle"
	_monitor_viewport.disable_3d = not piloting
	_monitor_viewport.render_target_update_mode = SubViewport.UPDATE_ONCE if _context == "room" else SubViewport.UPDATE_ALWAYS
	_monitor_viewport.msaa_3d = get_viewport().msaa_3d
	_monitor_viewport.scaling_3d_scale = float(UserSettings.get_value("render_scale"))
	_signal_filter.visible = piloting and _signal_filter.material != null and UserSettings.get_value("signal_effects")
	if _context == "terminal":
		terminal.open()
	elif _context == "room":
		terminal.standby()
	else:
		terminal.close()
	_hint.visible = _context in ["room", "spectator"]
	_identity.visible = piloting
	_controls.visible = piloting
	var images: Array = state["identification_images"]
	_vehicle_images.visible = piloting and not images.is_empty()
	for index: int in _portraits.size():
		_portraits[index].texture = images[index] if index < images.size() else null
		_portraits[index].visible = index < images.size()
	if _context != "room":
		_set_hovered(false)
	if piloting:
		_copy_camera_settings()
		_identity.text = "%s / %s" % [state["target_name"], state["camera_name"]]
		var movement := "%s / %s — рух   %s / %s — поворот" % [UserSettings.binding_text("forwardy"), UserSettings.binding_text("backwardy"), UserSettings.binding_text("lefty"), UserSettings.binding_text("righty")]
		if state["vertical_control"]:
			movement += "   %s / %s — підйом / зниження" % [UserSettings.binding_text("spacy"), UserSettings.binding_text("flight_descend")]
		_controls.text = "%s\n%s — камера   Esc — до кімнати" % [movement, UserSettings.binding_text("CameraChange")]
		if state["look_control"]:
			_controls.text += "   %s / %s — огляд" % [UserSettings.binding_text("CameraLeft"), UserSettings.binding_text("CameraRight")]
		if not state["input_armed"]:
			_controls.text += "   Відпустіть клавіші руху"
	elif _context == "spectator":
		_hint.text = "Вільна камера / режим розробника · Esc — до кімнати"
	else:
		_hint.text = "Натисніть, щоб відкрити термінал" if hovered else "Наведіть на монітор"
	_set_work_view(_context in ["terminal", "vehicle"], bool(UserSettings.get_value("reduced_motion")))

func _copy_camera_settings() -> void:
	if not is_instance_valid(_source_camera):
		return
	for property: String in ["projection", "keep_aspect", "fov", "size", "near", "far", "frustum_offset", "environment", "attributes"]:
		_feed_camera.set(property, _source_camera.get(property))
	_feed_camera.cull_mask = _source_camera.cull_mask & ~int(controller.get_room_layer())
	_feed_camera.global_transform = _source_camera.get_camera_transform()

func _process(_delta: float) -> void:
	if _context == "vehicle" and is_instance_valid(_source_camera):
		_feed_camera.global_transform = _source_camera.get_camera_transform()

func _set_work_view(active: bool, immediate: bool = false) -> void:
	if _close_view == active and not immediate:
		return
	_close_view = active
	if _pose_animation != null:
		_pose_animation.kill()
	var pose := _work_view_pose() if active else _overview
	var fov := 42.0 if active else _overview_fov
	if immediate or UserSettings.get_value("reduced_motion"):
		view_camera.global_transform = pose
		view_camera.fov = fov
	else:
		_pose_animation = create_tween().set_parallel()
		_pose_animation.set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
		_pose_animation.tween_property(view_camera, "global_transform", pose, 0.22)
		_pose_animation.tween_property(view_camera, "fov", fov, 0.22)

func _resize_work_view() -> void:
	if _close_view:
		_set_work_view(true, true)

func _work_view_pose() -> Transform3D:
	var points: Array[Vector3] = []
	for mesh: MeshInstance3D in [_housing, _keyboard_mesh]:
		var box := mesh.get_aabb()
		for index: int in 8:
			points.append(mesh.global_transform * box.get_endpoint(index))
	var framing := AABB(points[0], Vector3.ZERO)
	for point: Vector3 in points:
		framing = framing.expand(point)
	var center := framing.get_center()
	var normal := monitor.global_basis.inverse().transposed() * Vector3.BACK
	if normal.dot(_overview.origin - center) < 0.0:
		normal = -normal
	var direction := (normal.normalized() + Vector3.UP * 0.14).normalized()
	var basis := Basis.looking_at(-direction, Vector3.UP)
	var tangent := tan(deg_to_rad(42.0) * 0.5)
	var size := get_viewport().get_visible_rect().size
	var aspect := size.x / maxf(size.y, 1.0)
	var distance := 0.0
	for point: Vector3 in points:
		var local := basis.inverse() * (point - center)
		distance = maxf(distance, local.z + maxf(absf(local.x) / (tangent * aspect), absf(local.y) / tangent))
	return Transform3D(basis, center + direction * (distance * 1.05 + 0.025))

func _monitor_hit(point: Vector2) -> Dictionary:
	if view_camera == null or not view_camera.current or _pick_body == null:
		return {}
	var start := view_camera.project_ray_origin(point)
	var end := start + view_camera.project_ray_normal(point) * view_camera.far
	var query := PhysicsRayQueryParameters3D.create(start, end, int(controller.get_room_layer()))
	var hit := get_world_3d().direct_space_state.intersect_ray(query)
	return hit if not hit.is_empty() and hit["collider"] == _pick_body else {}

func monitor_at(point: Vector2) -> bool:
	return not _monitor_hit(point).is_empty()

func monitor_uv(point: Vector2) -> Vector2:
	var hit := _monitor_hit(point)
	if hit.is_empty():
		return Vector2(-1, -1)
	var local: Vector3 = monitor.to_local(hit["position"])
	for offset: int in range(0, _screen_indices.size(), 3):
		var ia := _screen_indices[offset]
		var ib := _screen_indices[offset + 1]
		var ic := _screen_indices[offset + 2]
		var edge0 := _screen_vertices[ib] - _screen_vertices[ia]
		var edge1 := _screen_vertices[ic] - _screen_vertices[ia]
		var relative := local - _screen_vertices[ia]
		var aa := edge0.dot(edge0)
		var ab := edge0.dot(edge1)
		var bb := edge1.dot(edge1)
		var denominator := aa * bb - ab * ab
		if absf(denominator) < 0.0000001:
			continue
		var u := (bb * relative.dot(edge0) - ab * relative.dot(edge1)) / denominator
		var v := (aa * relative.dot(edge1) - ab * relative.dot(edge0)) / denominator
		if u >= -0.001 and v >= -0.001 and u + v <= 1.001:
			return _screen_uvs[ia] * (1.0 - u - v) + _screen_uvs[ib] * u + _screen_uvs[ic] * v
	return Vector2(-1, -1)

func _physics_process(_delta: float) -> void:
	if terminal != null and _context == "room":
		_set_hovered(monitor_at(get_viewport().get_mouse_position()))

func _input(event: InputEvent) -> void:
	if keyboard != null and event is InputEventKey:
		keyboard.handle_key(event)
	if terminal == null or _context != "terminal" or event.is_action("ui_cancel"):
		return
	if event is InputEventKey:
		_monitor_viewport.push_input(event, true)
		get_viewport().set_input_as_handled()
	elif event is InputEventMouse:
		var uv := monitor_uv(event.position)
		if uv.x >= 0.0:
			var mapped := event.duplicate() as InputEventMouse
			mapped.position = uv * Vector2(_monitor_viewport.size_2d_override)
			mapped.global_position = mapped.position
			_monitor_viewport.notify_mouse_entered()
			_monitor_viewport.push_input(mapped, true)
			get_viewport().set_input_as_handled()
		terminal._restore_focus.call_deferred()

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
	if _hover_shell == null:
		return
	if _hover_animation != null:
		_hover_animation.kill()
	_hover_shell.visible = true
	_hover_animation = create_tween()
	_hover_animation.tween_property(_hover_shell.material_override, "albedo_color:a", 0.42 if hovered else 0.0, 0.0 if UserSettings.get_value("reduced_motion") else 0.1)
	if not hovered:
		_hover_animation.tween_callback(func() -> void: _hover_shell.visible = false)
