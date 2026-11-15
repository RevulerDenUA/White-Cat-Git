extends SceneTree
## Exercise presentation changes through real input, without replacing the gameplay session.
var checks := 0
var failures := 0
var room: Node
var controller: Node

func _initialize() -> void:
	_run.call_deferred()

func check(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		push_error(message)

func frames(count: int = 4) -> void:
	for index: int in count:
		await physics_frame
		await process_frame
	await create_timer(0.0).timeout

func key(code: int, pressed: bool, character: int = 0, echo: bool = false) -> void:
	var event := InputEventKey.new()
	event.physical_keycode = code
	event.keycode = code
	event.unicode = character
	event.pressed = pressed
	event.echo = echo
	Input.parse_input_event(event)

func toggle(code: int = KEY_F10) -> void:
	key(code, true)
	key(code, false)
	await frames()

func click(control: Control) -> void:
	var logical_point := control.get_global_rect().get_center()
	if not room.is_display_expanded():
		var uv := logical_point / Vector2(room.get_display_viewport().size_2d_override)
		var indices: PackedInt32Array = room._screen_indices
		var coordinates: PackedVector2Array = room._screen_uvs
		for offset: int in range(0, indices.size(), 3):
			var a := indices[offset]
			var b := indices[offset + 1]
			var c := indices[offset + 2]
			# UV triangles are planar: barycentric weights also locate the authored glass.
			var ab := coordinates[b] - coordinates[a]
			var ac := coordinates[c] - coordinates[a]
			var relative := uv - coordinates[a]
			var u := relative.cross(ac) / ab.cross(ac)
			var v := ab.cross(relative) / ab.cross(ac)
			var bary := Vector3(1.0 - u - v, u, v)
			if bary.x >= -0.001 and bary.y >= -0.001 and bary.z >= -0.001:
				var vertices: PackedVector3Array = room._screen_vertices
				var local := vertices[a] * bary.x + vertices[b] * bary.y + vertices[c] * bary.z
				logical_point = room.view_camera.unproject_position(room.monitor.to_global(local))
				break
	var point := root.get_final_transform() * logical_point
	var motion := InputEventMouseMotion.new()
	motion.position = point
	Input.parse_input_event(motion)
	await frames()
	var event := InputEventMouseButton.new()
	event.position = point
	event.button_index = MOUSE_BUTTON_LEFT
	event.pressed = true
	Input.parse_input_event(event)
	event = event.duplicate()
	event.pressed = false
	Input.parse_input_event(event)
	await frames()

func _run() -> void:
	var settings: Node = root.get_node("UserSettings")
	var baseline: Dictionary = settings.values.duplicate(true)
	var bindings: Dictionary = settings.bindings.duplicate(true)
	var reduced: Dictionary = baseline.duplicate(true)
	reduced["reduced_motion"] = true
	settings.save_values(reduced, bindings)
	var game: Node = load("res://Scene/A.tscn").instantiate()
	root.add_child(game)
	current_scene = game
	await frames()
	room = game.get_node("CommandRoom")
	controller = game.get_node("CameraControl")
	var display: SubViewport = room.get_display_viewport()
	check(not room.is_display_expanded(), "The room must initially show the authored physical monitor")
	await toggle()
	check(not room.is_display_expanded() and controller.snapshot()["context"] == "room", "Presentation shortcut opened a session from the room")
	controller.open_terminal()
	await frames()
	room.execute_command("help")
	var history: String = room.terminal.output.text
	room.terminal.entry.text = "connect ingenu"
	room.terminal.entry.caret_column = 7
	var caret: int = room.terminal.entry.caret_column
	await toggle()
	check(room.is_display_expanded(), "F10 did not expand the terminal")
	check(room.terminal.entry.text == "connect ingenu" and room.terminal.entry.caret_column == caret, "Expanding destroyed the command draft or caret")
	check(room.terminal.output.text == history, "Expanding rewrote actual terminal history")
	check(display.gui_get_focus_owner() == room.terminal.entry and room.terminal.entry.is_editing(), "Expanded terminal lost automatic editing focus")
	check(room._expanded_surface.texture == display.get_texture(), "Expanded terminal uses a second render target")
	key(KEY_F10, true, 0, true)
	key(KEY_F10, false)
	await frames()
	check(room.is_display_expanded(), "Holding the presentation shortcut repeatedly toggles the screen")
	key(KEY_W, true, 119)
	key(KEY_W, false)
	await frames()
	check("w" in room.terminal.entry.text, "Typing in the expanded terminal requires a second click")
	check(is_zero_approx(game.get_node("CharacterBody3D").engine_force), "Expanded terminal typing moved the rover")
	await click(room.terminal.display_toggle)
	check(not room.is_display_expanded(), "Expanded terminal's return-to-monitor button did not receive mapped pointer input")
	check(display.size == room.display_size and display.size_2d_override == Vector2i(1280, 502), "Physical screen dimensions were not restored")
	await click(room.terminal.display_toggle)
	check(room.is_display_expanded(), "Physical terminal's expand button did not receive authored monitor UV input")
	var addresses: Array[String] = []
	for target: Dictionary in controller.catalog():
		for camera: Dictionary in target["cameras"]:
			addresses.append(camera["address"])
	for address: String in addresses:
		check(controller.connect_target(address).is_empty(), "Real camera rejected: " + address)
		await frames()
		var before: Dictionary = controller.snapshot()
		check(room.is_display_expanded(), "Connecting a vehicle reset the selected display mode")
		check(room._source_camera == controller.get_active_camera() and room._feed_camera.current, "Expanded feed does not follow selected real camera")
		check(root.get_camera_3d() == room.view_camera, "Presentation toggle replaced the root room camera")
		check(not room.terminal.visible and not room.terminal.entry.has_focus(), "Terminal is still typing during fullscreen piloting")
		for portrait: TextureRect in room._portraits:
			check(portrait.texture != null and portrait.visible, "Vehicle identification cutout disappeared in fullscreen")
		await toggle()
		check(not room.is_display_expanded() and controller.snapshot() == before, "Returning to the monitor changed control, camera or input arming")
		await toggle()
		check(room.is_display_expanded() and controller.snapshot() == before, "Expanding changed the gameplay session")
	controller.connect_target("rover")
	await frames()
	key(KEY_C, true)
	key(KEY_C, false)
	await frames()
	check(controller.snapshot()["camera_id"] == "carcamera2" and room.is_display_expanded(), "Fullscreen next-camera shortcut changed presentation or stopped working")
	key(KEY_W, true)
	await frames(20)
	check(game.get_node("CharacterBody3D").engine_force > 0, "Fullscreen rover is not receiving normal WASD input")
	var before: Dictionary = controller.snapshot()
	await toggle()
	check(controller.snapshot() == before and game.get_node("CharacterBody3D").engine_force > 0, "Changing presentation while moving interrupted rover control")
	key(KEY_W, false)
	await frames()
	await toggle()
	await click(room._view_toggle)
	check(not room.is_display_expanded(), "Vehicle return-to-monitor button did not receive fullscreen pointer input")
	await click(room._view_toggle)
	check(room.is_display_expanded(), "Vehicle expand button did not receive authored monitor UV input")
	controller.connect_target("ingenuity/camcolor")
	await frames()
	key(KEY_SPACE, true)
	await frames(20)
	check(game.get_node("FlyingJeep").velocity.y > 0, "Expanded Ingenuity is not receiving vertical control")
	key(KEY_SPACE, false)
	for size: Vector2i in [Vector2i(1280, 720), Vector2i(2560, 1080), Vector2i(1920, 1440)]:
		root.size = size
		await frames()
		var logical := Vector2i(root.get_visible_rect().size)
		check(display.size_2d_override == logical, "Fullscreen HUD did not follow host viewport at " + str(size))
		check(display.size == Vector2i(root.get_texture().get_size()), "Fullscreen render target has the wrong pixel dimensions at " + str(size))
		check(room._expanded_surface.get_rect().size.is_equal_approx(Vector2(logical)), "Fullscreen surface crops or stretches the host viewport")
		for control: Control in [room._view_toggle, room._identity, room._vehicle_images, room._controls]:
			check(Rect2(Vector2.ZERO, Vector2(logical)).grow(2).encloses(control.get_global_rect()), "Fullscreen control escaped the safe area: " + control.name)
	var custom: Dictionary = bindings.duplicate(true)
	custom["command_display"] = KEY_V
	check(settings.binding_conflict("command_display", KEY_W, custom) == "forwardy", "Presentation action can collide with movement")
	check(settings.save_values(reduced, custom) == OK, "Presentation action could not be rebound")
	await frames()
	check("V —" in room._view_toggle.text, "Display button does not show actual remapped shortcut")
	await toggle(KEY_F10)
	check(room.is_display_expanded(), "Old default shortcut still changes presentation after rebinding")
	await toggle(KEY_V)
	check(not room.is_display_expanded(), "Remapped display shortcut did not work while piloting")
	key(KEY_ESCAPE, true)
	key(KEY_ESCAPE, false)
	await frames()
	controller.open_terminal()
	await frames()
	room.terminal.entry.text = "help"
	await toggle(KEY_V)
	check(room.is_display_expanded() and room.terminal.entry.text == "help", "Remapped printable shortcut leaked into terminal text")
	key(KEY_ESCAPE, true)
	key(KEY_ESCAPE, false)
	await frames()
	check(controller.snapshot()["context"] == "room" and not room.is_display_expanded(), "Esc from fullscreen terminal did not return to the real room")
	check(display.size == room.display_size and room.terminal.output.text == history, "Standby monitor lost its normal resolution or retained output")
	controller.connect_target("rover")
	await frames()
	game.get_node("CharacterBody3D").queue_free()
	await frames()
	check(controller.snapshot()["context"] == "room" and not room.is_display_expanded(), "Deleting an expanded selected vehicle left the fullscreen display active")
	# A player's pre-existing F10 camera binding must survive migration of the new action.
	settings.bindings = settings.default_bindings()
	settings.bindings["CameraChange"] = KEY_F10
	var saved: Dictionary = settings.bindings.duplicate()
	saved.erase("command_display")
	settings._preserve_saved_bindings(saved)
	check(settings.bindings["CameraChange"] == KEY_F10 and settings.bindings["command_display"] != KEY_F10, "New default erased an existing saved shortcut")
	check(settings.binding_conflict("command_display", settings.bindings["command_display"], settings.bindings).is_empty(), "Migrated presentation shortcut conflicts with existing controls")
	current_scene = null
	game.queue_free()
	await frames()
	settings.save_values(baseline, bindings)
	print("DISPLAY MODES: %d checks; %d failures" % [checks, failures])
	quit(1 if failures else 0)
