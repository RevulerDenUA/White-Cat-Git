extends SceneTree
func _initialize() -> void:
	_run.call_deferred()
func _run() -> void:
	var size := Vector2i(1600, 900)
	var mode := "room"
	var scene := "res://Scene/A.tscn"
	var path := "/tmp/white-cat-command-room.png"
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--size="):
			var parts := arg.trim_prefix("--size=").split("x")
			size = Vector2i(int(parts[0]), int(parts[1]))
		elif arg.begins_with("--mode="):
			mode = arg.trim_prefix("--mode=")
		elif arg.begins_with("--scene="):
			scene = arg.trim_prefix("--scene=")
		elif arg.begins_with("--output="):
			path = arg.trim_prefix("--output=")
	var viewport := SubViewport.new()
	viewport.size = size
	var aspect := float(size.x) / float(size.y)
	viewport.size_2d_override = Vector2i(roundi(1080 * aspect), 1080) if aspect >= 1920.0 / 1080.0 else Vector2i(1920, roundi(1920.0 / aspect))
	viewport.size_2d_override_stretch = true
	viewport.own_world_3d = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	viewport.msaa_3d = Viewport.MSAA_2X
	root.add_child(viewport)
	var game: Node = load(scene).instantiate()
	viewport.add_child(game)
	await create_timer(1.0).timeout
	var controller: Node = game.get_node("CameraControl")
	var room: Node = game.get_node("CommandRoom")
	if mode == "terminal" or mode == "terminal-fullscreen":
		controller.open_terminal()
		room.execute_command("help")
		if mode == "terminal-fullscreen":
			room.execute_command("fullscreen")
	elif mode == "telescope":
		controller.open_terminal()
		room.execute_command("telescope")
	elif mode == "hover":
		room.set_physics_process(false)
		room._set_hovered(true)
	elif mode != "room":
		controller.connect_target(mode)
	await create_timer(0.3).timeout
	await RenderingServer.frame_post_draw
	DirAccess.make_dir_recursive_absolute(path.get_base_dir())
	var error := viewport.get_texture().get_image().save_png(path)
	print("COMMAND CAPTURE: %s (%s)" % [path, error_string(error)])
	quit(0 if error == OK else 1)
