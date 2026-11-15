extends SceneTree
## Terminal fullscreen (command + action) and the telescope command contract.
var checks := 0
var failures := 0
var game: Node
var controller: Node
var room: Node

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

func tap_action(name: String) -> void:
	action(name, true)
	await frames(2)
	action(name, false)
	await frames(2)

func key(code: int, pressed: bool) -> void:
	var event := InputEventKey.new()
	event.physical_keycode = code
	event.keycode = code
	event.pressed = pressed
	Input.parse_input_event(event)

func click(point: Vector2) -> void:
	for pressed: bool in [true, false]:
		var event := InputEventMouseButton.new()
		event.button_index = MOUSE_BUTTON_LEFT
		event.pressed = pressed
		event.position = point
		event.global_position = point
		Input.parse_input_event(event)
		await frames(1)

func terminal_has_focus() -> bool:
	return room.terminal.entry.has_focus()

func _run() -> void:
	game = load("res://Scene/A.tscn").instantiate()
	root.add_child(game)
	current_scene = game
	controller = game.get_node("CameraControl")
	room = game.get_node("CommandRoom")
	await frames()
	var viewport: SubViewport = room.get_display_viewport()
	var monitor_size: Vector2i = viewport.size
	var monitor_logical: Vector2i = viewport.size_2d_override
	check(InputMap.has_action("terminal_fullscreen"), "Fullscreen action is not registered")
	check(InputMap.action_get_events("terminal_fullscreen").size() > 0, "Fullscreen action has no binding")

	# The action does nothing in the room.
	await tap_action("terminal_fullscreen")
	check(not room.is_fullscreen(), "Fullscreen opened from the room")

	controller.open_terminal()
	await frames()
	check(controller.snapshot()["context"] == "terminal", "Terminal did not open")
	check(terminal_has_focus(), "Terminal input has no focus after opening")

	# help lists the new commands.
	room.execute_command("help")
	var help_text: String = room.terminal.output.text
	check("telescope" in help_text, "help does not list telescope")
	check("fullscreen" in help_text, "help does not list fullscreen")

	# ---- fullscreen via command ----
	room.execute_command("fullscreen")
	await frames()
	check(room.is_fullscreen(), "fullscreen command did not expand the display")
	check(room._fullscreen_layer.visible, "Fullscreen layer is hidden")
	var screen: Vector2 = root.get_visible_rect().size
	var logical: Vector2i = viewport.size_2d_override
	check(logical.x == 1280, "Fullscreen changed the logical width")
	check(absf(float(logical.x) / logical.y - screen.x / screen.y) < 0.02, "Fullscreen display does not match the window aspect")
	check(room.terminal.visible and terminal_has_focus(), "Terminal lost focus in fullscreen")
	# Mouse mapping: screen centre -> display centre.
	var mapped: Vector2 = room.display_point(screen * 0.5)
	check(mapped.distance_to(Vector2(logical) * 0.5) < 2.0, "Fullscreen mouse mapping is off: %s" % mapped)
	check(room.display_point(Vector2(-10, -10)).x < 0.0, "Point outside the window mapped into the display")
	# Clicking the output keeps typing possible without another click.
	await click(screen * 0.5)
	await frames(2)
	check(terminal_has_focus(), "Click on fullscreen output did not return focus to the command line")
	var output_rect: Rect2 = room.terminal.output.get_global_rect()
	check(output_rect.size.y > 502.0 * 0.5, "Fullscreen terminal output did not grow")

	# ---- fullscreen off via action ----
	await tap_action("terminal_fullscreen")
	check(not room.is_fullscreen(), "Fullscreen action did not return to the monitor")
	check(viewport.size == monitor_size and viewport.size_2d_override == monitor_logical, "Monitor size was not restored")
	check(not room._fullscreen_layer.visible, "Fullscreen layer stayed visible")
	await tap_action("terminal_fullscreen")
	check(room.is_fullscreen(), "Fullscreen action did not expand the display")
	await tap_action("terminal_fullscreen")

	# ---- telescope ----
	room.execute_command("telescope")
	await frames()
	var telescope: Control = room.get_telescope()
	check(telescope != null and telescope.is_visible_in_tree(), "telescope command did not open the telescope")
	check(not room.terminal.visible, "Terminal stayed visible under the telescope")
	check(not terminal_has_focus(), "Command line kept focus while the telescope is open")
	check(telescope != null and telescope.get_parent() == room.terminal.get_parent(), "Telescope is not inside the display")
	# Keys reach the telescope and the stub closes on Backspace.
	key(KEY_BACKSPACE, true)
	await frames(2)
	key(KEY_BACKSPACE, false)
	await frames(3)
	check(not room.is_telescope_open(), "Closed signal did not close the telescope")
	check(room.terminal.visible and terminal_has_focus(), "Terminal did not get focus back after the telescope")
	check(controller.snapshot()["context"] == "terminal", "Closing the telescope left the terminal context")

	# ---- telescope in fullscreen, then Esc ----
	room.execute_command("telescope")
	await frames()
	await tap_action("terminal_fullscreen")
	check(room.is_fullscreen() and room.is_telescope_open(), "Telescope could not go fullscreen")
	await tap_action("ui_cancel")
	await frames(2)
	check(controller.snapshot()["context"] == "room", "Esc did not return to the room")
	check(not room.is_fullscreen(), "Returning to the room kept fullscreen")
	check(not room.is_telescope_open(), "Returning to the room kept the telescope")
	check(viewport.size == monitor_size and viewport.size_2d_override == monitor_logical, "Room monitor size was not restored")
	check(room.terminal.visible, "Terminal standby text is hidden in the room")

	# ---- vehicle feed fullscreen ----
	controller.open_terminal()
	await frames()
	var error: String = controller.connect_target("rover")
	check(error.is_empty(), "Could not connect the rover: " + error)
	await frames()
	await tap_action("terminal_fullscreen")
	check(room.is_fullscreen(), "Vehicle feed did not go fullscreen")
	check("на монітор" in room._controls.text, "Pilot hint does not mention leaving fullscreen")
	await tap_action("ui_cancel")
	check(controller.snapshot()["context"] == "room" and not room.is_fullscreen(), "Esc from vehicle fullscreen did not reset")

	print("TERMINAL FULLSCREEN: %d checks; %d failures" % [checks, failures])
	quit(1 if failures > 0 else 0)
