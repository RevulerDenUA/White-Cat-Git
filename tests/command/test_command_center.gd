extends SceneTree
var checks := 0
var failures := 0
var game: Node
var controller: Node
var room: Node
var settings: Node
var recovery_requests := 0

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

func action(name: String, pressed: bool) -> void:
	var event := InputEventAction.new()
	event.action = name
	event.pressed = pressed
	Input.parse_input_event(event)

func key(code: int, pressed: bool, unicode_value: int = 0) -> void:
	var event := InputEventKey.new()
	event.physical_keycode = code
	event.keycode = code
	event.unicode = unicode_value
	event.pressed = pressed
	Input.parse_input_event(event)

func _run() -> void:
	settings = root.get_node("UserSettings")
	var baseline: Dictionary = settings.values.duplicate(true)
	var bindings: Dictionary = settings.bindings.duplicate(true)
	game = load("res://Scene/A.tscn").instantiate()
	root.add_child(game)
	current_scene = game
	controller = game.get_node("CameraControl")
	room = game.get_node("CommandRoom")
	await frames()
	check(controller.snapshot()["context"] == "room", "Game did not start in the command room")
	check(root.get_camera_3d() == room.view_camera, "Authored room camera is not current")
	for light: Node in room.find_children("*", "OmniLight3D", true, false):
		check(bool(light.layers & room.view_camera.cull_mask), "Authored room light was excluded by camera mask")
	check(Input.mouse_mode == Input.MOUSE_MODE_VISIBLE, "Spectator captured mouse in room")
	check(not room._vehicle_images.visible, "Vehicle reference images appeared in the room")
	check(controller.catalog().size() == 2, "Two real vehicles were not registered")
	game.get_node("CharacterBody3D/RemoteVehicle").RecoveryRequested.connect(func() -> void: recovery_requests += 1)
	var addresses: Array[String] = []
	for target: Dictionary in controller.catalog():
		for camera: Dictionary in target["cameras"]:
			addresses.append(camera["address"])
	check(addresses.size() == 4, "Updated scene did not expose its four real cameras")
	check(not controller.connect_target("rover/carcamera3").is_empty(), "Removed upstream camera was silently recreated")
	var center := Vector3.ZERO
	var arrays: Array = room.monitor.mesh.surface_get_arrays(room._surface)
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	for vertex: Vector3 in vertices:
		center += room.monitor.global_transform * vertex
	center /= vertices.size()
	var point: Vector2 = room.view_camera.unproject_position(center)
	check(room.monitor_at(point), "Ray at authored display did not hit interaction surface")
	check(not room.monitor_at(Vector2(10, 10)), "Ray outside monitor entered terminal")
	var click := InputEventMouseButton.new()
	click.position = point
	click.button_index = MOUSE_BUTTON_LEFT
	click.pressed = true
	room._unhandled_input(click)
	await frames()
	check(controller.snapshot()["context"] == "terminal", "One monitor click did not open terminal")
	check(root.gui_get_focus_owner() == room.terminal.entry, "Terminal did not focus command input")
	check(root.get_camera_3d() == room.view_camera, "Opening terminal changed the scene camera")
	room.terminal.entry.text_submitted.emit("help")
	check("connect rover" in room.terminal.output.text and "ingenuity/camcolor" in room.terminal.output.text, "help did not list real vehicle/camera addresses")
	key(KEY_W, true, 119)
	key(KEY_C, true, 99)
	await frames()
	check(controller.snapshot()["context"] == "terminal", "Typing camera shortcut escaped terminal")
	check(is_zero_approx(game.get_node("CharacterBody3D").engine_force), "Typing in terminal applied engine force")
	check(game.get_node("FlyingJeep").velocity.is_zero_approx(), "Typing in terminal moved ingenuity")
	key(KEY_C, false)
	room.terminal.entry.text_submitted.emit("connect rover")
	await frames()
	check(controller.snapshot()["target_id"] == "rover", "connect did not grant vehicle control")
	check(not controller.snapshot()["input_armed"], "Held terminal movement key leaked into piloting")
	check(is_zero_approx(game.get_node("CharacterBody3D").engine_force), "Held key moved rover after connect")
	key(KEY_W, false)
	await frames()
	check(controller.snapshot()["input_armed"], "Piloting did not arm after movement key release")
	key(KEY_W, true)
	await frames(20)
	check(game.get_node("CharacterBody3D").engine_force > 0, "Connected rover did not receive movement input")
	controller.notification(Node.NOTIFICATION_APPLICATION_FOCUS_OUT)
	check(not controller.snapshot()["input_armed"], "Losing window focus did not disarm piloting")
	check("Відпустіть клавіші руху" in room._controls.text, "HUD did not report input disarmed after focus loss")
	await frames()
	check(is_zero_approx(game.get_node("CharacterBody3D").engine_force), "Held movement continued driving after focus loss")
	key(KEY_W, false)
	await frames()
	check(controller.snapshot()["input_armed"] and not "Відпустіть клавіші руху" in room._controls.text, "Input and HUD did not recover together after held keys were released")
	controller.connect_target("rover/carcamera2")
	check(recovery_requests == 0, "Changing camera on same target requested recovery")
	for address: String in addresses:
		check(controller.connect_target(address).is_empty(), "Camera address rejected: " + address)
		await frames()
		var state: Dictionary = controller.snapshot()
		check(state["target_id"] + "/" + state["camera_id"] == address, "Camera and control recipient diverged: " + address)
		check(root.get_camera_3d().name.to_lower() == state["camera_id"], "Current camera did not match snapshot")
		check(room._vehicle_images.visible and room._portraits.size() == 2, "Vehicle reference images are missing while piloting")
		check(room._portraits[0].texture == state["identification_images"][0] and room._portraits[1].texture == state["identification_images"][1], "Reference images do not belong to controlled vehicle")
		check(not root.get_camera_3d().cull_mask & controller.get_room_layer(), "Vehicle camera can see isolated room model")
	check(recovery_requests == 1, "Changing controlled target did not request automatic rover recovery exactly once")
	var before: Dictionary = controller.snapshot()
	check(not controller.connect_target("missing").is_empty() and controller.snapshot() == before, "Invalid target changed live session")
	check(not controller.connect_target("rover/missing").is_empty() and controller.snapshot() == before, "Invalid camera changed live session")
	check(not controller.connect_target("rover/carcamera/extra").is_empty(), "Malformed address was accepted")
	controller.connect_target("ingenuity/cambw")
	await frames()
	key(KEY_SPACE, true)
	await frames(20)
	check(game.get_node("FlyingJeep").velocity.y > 0, "Connected ingenuity did not receive vertical input")
	key(KEY_SPACE, false)
	controller.return_to_room()
	await frames(80)
	check(game.get_node("FlyingJeep").velocity.is_zero_approx(), "Uncontrolled ingenuity did not retain existing deceleration")
	var custom: Dictionary = bindings.duplicate(true)
	custom["forwardy"] = KEY_Z
	custom["DrillPickup"] = KEY_R
	custom["CameraChange"] = KEY_X
	check(settings.save_values(baseline, custom) == OK, "Remapped controls were rejected")
	controller.connect_target("rover")
	await frames()
	check("Z / S" in room._controls.text and "X — камера" in room._controls.text, "HUD ignored actual remapped bindings")
	key(KEY_Z, true)
	await frames(20)
	check(game.get_node("CharacterBody3D").engine_force > 0, "Remapped movement did not drive rover")
	key(KEY_Z, false)
	key(KEY_X, true)
	await frames()
	check(controller.snapshot()["camera_id"] == "carcamera2", "Remapped next-camera action did not cycle within connected target")
	key(KEY_X, false)
	key(KEY_ESCAPE, true)
	await frames()
	check(controller.snapshot()["context"] == "room" and root.get_camera_3d() == room.view_camera, "Esc did not return to authored room camera")
	check(not room._vehicle_images.visible, "Vehicle images remained visible after leaving piloting")
	key(KEY_ESCAPE, false)
	action("SpectatorMode", true)
	await frames()
	check(controller.snapshot()["context"] == "room", "Developer camera enabled without developer mode")
	action("SpectatorMode", false)
	var developer: Dictionary = baseline.duplicate(true)
	developer["developer_mode"] = true
	developer["reduced_motion"] = true
	settings.save_values(developer, custom)
	action("SpectatorMode", true)
	await frames()
	check(controller.snapshot()["context"] == "spectator", "Developer camera failed to activate")
	action("SpectatorMode", false)
	settings.save_values(baseline, custom)
	await frames()
	check(controller.snapshot()["context"] == "room", "Disabling developer mode left free camera active")
	for size: Vector2i in [Vector2i(1920, 1080), Vector2i(2560, 1080), Vector2i(1920, 1440), Vector2i(1920, 4160)]:
		root.size = size
		controller.connect_target("rover")
		await frames()
		var bounds := Rect2(Vector2.ZERO, root.get_visible_rect().size).grow(2)
		check(bounds.encloses(room._vehicle_images.get_global_rect()), "Vehicle photos left top-right viewport at " + str(size))
		check(room._vehicle_images.is_visible_in_tree(), "Reference photos disappeared at " + str(size))
		controller.return_to_room()
		controller.open_terminal()
		await frames()
		var rect := Rect2(Vector2.ZERO, root.get_visible_rect().size).grow(2)
		check(root.gui_get_focus_owner() == room.terminal.entry, "Terminal lost focus at " + str(size))
		for node: Node in room.terminal.find_children("*", "Control", true, false):
			if node.is_visible_in_tree():
				check(rect.encloses(node.get_global_rect()), "Terminal control clipped: " + str(node.name) + " at " + str(size))
		controller.return_to_room()
	controller.connect_target("ingenuity/camcolor")
	game.get_node("FlyingJeep/CamColor").queue_free()
	await frames()
	check(controller.snapshot()["context"] == "room", "Removing active camera did not release control")
	check(controller.catalog()[1]["cameras"].size() == 1, "Removed camera remained in registry")
	controller.connect_target("rover")
	game.get_node("CharacterBody3D").queue_free()
	await frames()
	check(controller.snapshot()["context"] == "room" and controller.catalog().size() == 1, "Removing controlled vehicle left stale control or registry")
	var dynamic := CharacterBody3D.new()
	dynamic.name = "LateVehicle"
	var camera := Camera3D.new()
	camera.name = "View"
	dynamic.add_child(camera)
	var adapter: Node = load("res://Scripts/Command/RemoteVehicle.cs").new()
	adapter.TargetId = "late_vehicle"
	adapter.DisplayName = "Тестовий апарат"
	adapter.Cameras = [camera] as Array[Camera3D]
	dynamic.add_child(adapter)
	game.add_child(dynamic)
	await frames()
	check(controller.catalog().size() == 2 and controller.connect_target("late_vehicle/view").is_empty(), "Late vehicle did not register without hardcoded list")
	dynamic.queue_free()
	await frames()
	check(controller.snapshot()["context"] == "room" and controller.catalog().size() == 1, "Dynamic vehicle removal left stale state")
	settings.save_values(baseline, bindings)
	current_scene = null
	game.queue_free()
	await frames()
	print("COMMAND CENTER: %d checks; %d failures" % [checks, failures])
	quit(1 if failures else 0)
