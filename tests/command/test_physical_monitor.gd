extends SceneTree
var checks := 0
var failures := 0

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
	# process_frame fires before Node._process; inspect the completed camera pose.
	await create_timer(0.0).timeout

func key(code: int, pressed: bool, character: int = 0) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.unicode = character
	event.pressed = pressed
	Input.parse_input_event(event)

func type_text(value: String) -> void:
	for character: String in value:
		var code := character.to_upper().unicode_at(0)
		key(code, true, character.unicode_at(0))
		key(code, false)
		await process_frame
	await frames()

func _run() -> void:
	var settings: Node = root.get_node("UserSettings")
	var baseline: Dictionary = settings.values.duplicate(true)
	var reduced: Dictionary = baseline.duplicate(true)
	reduced["reduced_motion"] = true
	settings.save_values(reduced, settings.bindings)
	var game: Node = load("res://Scene/A.tscn").instantiate()
	root.add_child(game)
	current_scene = game
	await frames()
	var controller: Node = game.get_node("CameraControl")
	var room: Node = game.get_node("CommandRoom")
	var display: SubViewport = room.get_display_viewport()
	check(room.keyboard.keys.size() == 41, "Authored keyboard was not split into its 41 real keys")
	check(room.terminal.visible and not room.terminal.entry.editable, "Small monitor does not show the actual terminal in standby")
	check(room._display_material.albedo_texture == display.get_texture(), "Physical screen is not using the real display viewport")
	var overview: Transform3D = room.view_camera.global_transform
	var center := Vector3.ZERO
	for vertex: Vector3 in room._screen_vertices:
		center += room.monitor.to_global(vertex)
	center /= room._screen_vertices.size()
	var point: Vector2 = room.view_camera.unproject_position(center)
	check(room.monitor_uv(point).distance_to(Vector2(0.5, 0.5)) < 0.1, "Monitor hit cannot map to its authored texture UV")
	room.set_physics_process(false)
	room._set_hovered(true)
	await frames()
	check(room._hover_shell.visible and room._hover_shell.material_override.albedo_color.a > 0.3, "Hover does not show the outer translucent housing halo")
	room.set_physics_process(true)
	controller.open_terminal()
	await frames()
	check(room.view_camera.current and not room.view_camera.global_transform.is_equal_approx(overview), "Terminal did not approach the physical monitor")
	check(not room._hover_shell.visible, "Hover halo remained over the active monitor")
	check(display.gui_get_focus_owner() == room.terminal.entry, "Terminal input is not focused automatically")
	room.terminal.entry.release_focus()
	await frames()
	check(display.gui_get_focus_owner() == room.terminal.entry, "Terminal did not restore lost input focus")
	await type_text("help")
	check(room.terminal.entry.text == "help", "Typing without a second click did not enter the terminal")
	key(KEY_ENTER, true, 13)
	key(KEY_ENTER, false)
	await frames()
	check("rover — " in room.terminal.output.text and room.terminal.entry.text.is_empty(), "Enter did not execute the real typed terminal command")
	check(room.terminal.entry.is_editing(), "Submitting a command ended automatic terminal editing")
	point = room.view_camera.unproject_position(center)
	var click := InputEventMouseButton.new()
	click.position = point
	click.button_index = MOUSE_BUTTON_LEFT
	click.pressed = true
	Input.parse_input_event(click)
	click = click.duplicate()
	click.pressed = false
	Input.parse_input_event(click)
	await frames()
	check(display.gui_get_focus_owner() == room.terminal.entry, "Clicking terminal output lost typing focus")
	var rest: Vector3 = room.keyboard._rests[KEY_W]
	key(KEY_W, true, 119)
	await frames()
	check(room.keyboard.keys[KEY_W].position.y < rest.y - 0.005, "The physical W key does not depress while held")
	check(is_zero_approx(game.get_node("CharacterBody3D").engine_force), "Terminal typing leaked into vehicle movement")
	key(KEY_W, false)
	await frames()
	check(room.keyboard.keys[KEY_W].position.is_equal_approx(rest), "The physical W key did not return after release")
	room.terminal.entry.clear()
	await type_text("connect rover")
	check(room.terminal.entry.text == "connect rover", "Typed command differs from input: " + room.terminal.entry.text)
	key(KEY_ENTER, true, 13)
	key(KEY_ENTER, false)
	await frames()
	# Тестовий полігон із термінала недоступний; потік на моніторі перевіряємо через API контролера
	check(controller.snapshot()["context"] == "terminal", "A typed connect reached the test ground vehicle")
	controller.connect_target("rover")
	await frames()
	check(controller.snapshot()["context"] == "vehicle", "A typed connect command did not grant piloting")
	check(root.get_camera_3d() == room.view_camera, "Piloting replaced the physical room/monitor view")
	check(not display.disable_3d and display.world_3d == root.world_3d, "Vehicle stream is not rendering the real scene world")
	check(room._feed_camera.current and room._source_camera == controller.get_active_camera(), "Feed source does not match the actual selected camera")
	check(room._source_camera != null and room._feed_camera.global_transform.is_equal_approx(room._source_camera.get_camera_transform()), "Feed camera does not follow its real source")
	check(not bool(room._feed_camera.cull_mask & controller.get_room_layer()), "Monitor feed renders its own room recursively")
	check(not room.terminal.visible and not room.terminal.entry.has_focus(), "Terminal captures keys during WASD piloting")
	for portrait: TextureRect in room._portraits:
		check(portrait.get_parent() == room._vehicle_images, "Vehicle cutout is still inside a decorative frame")
		check(portrait.texture != null and portrait.visible, "Transparent vehicle cutout is missing")
	for size: Vector2i in [Vector2i(1280, 720), Vector2i(2560, 1080), Vector2i(1920, 1440)]:
		root.size = size
		await frames()
		var bounds := Rect2(Vector2.ZERO, root.get_visible_rect().size).grow(2)
		for mesh: MeshInstance3D in [room._housing, room._keyboard_mesh]:
			for index: int in 8:
				var corner: Vector3 = mesh.to_global(mesh.get_aabb().get_endpoint(index))
				check(bounds.has_point(room.view_camera.unproject_position(corner)), "Monitor housing/keyboard clipped at " + str(size))
	controller.return_to_room()
	await frames()
	check(room.view_camera.global_transform.is_equal_approx(overview), "Leaving the monitor did not restore authored room overview")
	check(room.terminal.visible and not room.terminal.entry.editable, "Standby screen stopped showing retained terminal output")
	check("connect rover" in room.terminal.output.text, "Actual command history disappeared from the small screen")
	current_scene = null
	game.queue_free()
	await frames()
	settings.save_values(baseline, settings.bindings)
	print("PHYSICAL MONITOR: %d checks; %d failures" % [checks, failures])
	quit(1 if failures else 0)
