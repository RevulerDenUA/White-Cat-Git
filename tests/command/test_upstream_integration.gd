extends SceneTree
## Run only with a separate XDG_DATA_HOME: exercises the real upstream save file.
var checks := 0
var failures := 0
var game: Node
var settings: Node

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

func key(code: int, pressed: bool) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = pressed
	Input.parse_input_event(event)

func saved() -> Dictionary:
	return JSON.parse_string(FileAccess.get_file_as_string("user://save1.json"))

func _run() -> void:
	settings = root.get_node("UserSettings")
	var baseline: Dictionary = settings.values.duplicate(true)
	var bindings: Dictionary = settings.bindings.duplicate(true)
	# Simulate a settings file predating the newly imported drill actions.
	settings.bindings = settings.default_bindings()
	settings.bindings["CameraChange"] = KEY_X
	var old_actions := {}
	for action: String in bindings:
		if action not in ["CameraLeft", "CameraRight", "DrillPlace", "DrillConfirm", "interact", "DrillPickup"]:
			old_actions[action] = true
	settings._preserve_saved_bindings(old_actions)
	check(settings.bindings["CameraChange"] == KEY_X, "New defaults erased an existing camera shortcut")
	check(settings.bindings["DrillPickup"] != KEY_X, "New drill default conflicts with the saved camera shortcut")
	for action: String in settings.bindings:
		check(settings.binding_conflict(action, settings.bindings[action], settings.bindings).is_empty(), "Migrated action conflicts: " + action)
	settings.bindings = bindings.duplicate(true)
	check(settings.binding_conflict("DrillPlace", KEY_W, bindings) == "forwardy", "Drill actions do not conflict with vehicle movement")

	var custom: Dictionary = bindings.duplicate(true)
	custom["DrillPlace"] = KEY_P
	custom["DrillConfirm"] = KEY_T
	custom["DrillPickup"] = KEY_J
	custom["CameraLeft"] = KEY_U
	custom["CameraRight"] = KEY_O
	custom["flight_descend"] = KEY_Y
	var reduced: Dictionary = baseline.duplicate(true)
	reduced["reduced_motion"] = true
	check(settings.save_values(reduced, custom) == OK, "New upstream actions could not be rebound")
	DirAccess.remove_absolute("user://save1.json")
	game = load("res://Scene/A.tscn").instantiate()
	root.add_child(game)
	current_scene = game
	await frames()
	var controller: Node = game.get_node("CameraControl")
	var room: Node = game.get_node("CommandRoom")
	var manager: Node = game.get_node("DrillManager")
	var rover: VehicleBody3D = game.get_node("CharacterBody3D")
	var ingenuity: CharacterBody3D = game.get_node("FlyingJeep")
	var hud := room.get_display_viewport().find_child("ResourceHud", true, false) as Label
	var drill_hints := room.get_display_viewport().find_child("DrillHints", true, false) as Label
	var ghost := manager.get_node("DrillBlueprint") as Node3D
	var initial: Dictionary = saved()["Storage"]
	check(not hud.visible, "Resource HUD obscures the command room")
	check(not initial.is_empty(), "Upstream starter resources were not loaded")
	rover.freeze = true
	rover.global_transform = Transform3D(Basis.IDENTITY, Vector3(0, 2, 18))
	controller.connect_target("rover")
	await frames()
	check(not "\n" in hud.text and hud.max_lines_visible == 1, "Inventory is no longer a single compact row")
	check(hud.get_rect().end.y < 100 and drill_hints.get_rect().position.y > 350, "Resource HUD occupies the centre of the video feed")
	check(not "ціна" in drill_hints.text.to_lower(), "Idle driving still shows the full drill recipe")
	key(KEY_W, true)
	await frames(20)
	check(rover.engine_force > 0, "Updated delayed/throttled rover did not receive input")
	key(KEY_W, false)
	controller.connect_target("ingenuity")
	for index: int in 12:
		await frames(1)
		check(ingenuity.velocity.is_zero_approx(), "Queued rover command leaked into Ingenuity")
	key(KEY_SHIFT, true)
	await frames(20)
	check(is_zero_approx(ingenuity.velocity.y), "Hard-coded Shift bypassed the rebound descend action")
	key(KEY_SHIFT, false)
	key(KEY_Y, true)
	await frames(20)
	check(ingenuity.velocity.y < 0, "Rebound descend did not pass through delayed input")
	key(KEY_Y, false)
	controller.connect_target("rover/carcamera2")
	await frames(20)
	check(is_zero_approx(rover.engine_force), "Old throttle resumed without a new movement command")
	var camera := rover.get_node("CarCamera2") as Camera3D
	var camera_pose := camera.basis
	key(KEY_U, true)
	await frames(10)
	key(KEY_U, false)
	check(not camera.basis.is_equal_approx(camera_pose), "Existing camera look ignored its rebound key")
	check("U / O — огляд" in room._controls.text, "Camera look hint ignored real bindings")

	key(KEY_P, true)
	key(KEY_P, false)
	await frames()
	check(ghost.visible, "Rebound blueprint action did not open the real drill preview")
	check("[P]" in drill_hints.text, "Drill HUD still displays a hard-coded B shortcut")
	check(drill_hints.get_line_count() <= 2 and "Ціна:" in drill_hints.text, "Build preview does not keep its actual recipe in two footer lines")
	key(KEY_T, true)
	key(KEY_T, false)
	await frames()
	var after_build: Dictionary = saved()
	check(after_build["Drills"].size() == 1, "Real drill was not built on the supplied vein")
	for resource: String in initial:
		check(int(after_build["Storage"].get(resource, 0)) == 0, "Building a drill did not debit its actual cost")
	var drill: Node
	for child: Node in manager.get_children():
		if child.get_script() != null and child.get_script().resource_path == "res://Scripts/Drill/Drill.cs":
			drill = child
	if drill != null:
		drill.DeployTime = 0.0
		drill.ItemsPerMinute = 600.0
	controller.return_to_room()
	controller.open_terminal()
	key(KEY_P, true)
	key(KEY_P, false)
	await frames(40)
	check(not ghost.visible and not hud.visible, "Terminal input opened a drill preview or resource HUD")
	manager.Save()
	var during_terminal: Dictionary = saved()
	check(during_terminal["Storage"] == after_build["Storage"], "Typing a drill shortcut granted emergency resources in terminal")
	if drill != null:
		check(not during_terminal["Drills"][0]["Items"].is_empty(), "Deployed drill stopped producing after returning to the room")

	controller.connect_target("rover")
	rover.global_position = Vector3(0, 2, 20.5)
	await frames()
	key(KEY_G, true)
	key(KEY_G, false)
	await frames()
	check(int(saved()["Storage"].get("Залізна руда", 0)) > 0, "Collect action did not transfer produced resources")
	manager.PickupHoldTime = 0.1
	key(KEY_J, true)
	await frames(12)
	key(KEY_J, false)
	await frames()
	var recovered: Dictionary = saved()
	check(recovered["Drills"].is_empty(), "Holding the rebound pickup action did not dismantle the drill")
	for resource: String in initial:
		check(int(recovered["Storage"].get(resource, 0)) == floori(float(initial[resource]) * 0.5), "Dismantling did not refund 50 percent of " + resource)
	current_scene = null
	game.queue_free()
	await frames()
	game = load("res://Scene/A.tscn").instantiate()
	root.add_child(game)
	current_scene = game
	await frames()
	game.get_node("DrillManager").Save()
	var reloaded: Dictionary = saved()["Storage"]
	check(int(reloaded.get("Залізна руда", 0)) == int(recovered["Storage"].get("Залізна руда", 0)), "Reloading lost or duplicated harvested inventory")
	# Upstream explicitly tops up one build kit when no drills remain.
	for resource: String in initial:
		check(int(reloaded.get(resource, 0)) == int(initial[resource]), "Upstream emergency build-kit recovery changed unexpectedly")
	current_scene = null
	game.queue_free()
	await frames()
	game = load("res://Scene/WorldGen.tscn").instantiate()
	root.add_child(game)
	current_scene = game
	await frames(10)
	controller = game.get_node("CameraControl")
	check(controller.snapshot()["context"] == "room", "Updated terrain scene did not enter the command room")
	check(controller.catalog().size() == 1 and controller.catalog()[0]["cameras"].size() == 2, "Rover preset in terrain scene has an invalid adapter")
	check(controller.connect_target("rover/carcamera2").is_empty(), "Terrain rover cannot be controlled through the shared terminal controller")
	current_scene = null
	game.queue_free()
	await frames()
	settings.save_values(baseline, bindings)
	print("UPSTREAM INTEGRATION: %d checks; %d failures" % [checks, failures])
	quit(1 if failures else 0)
