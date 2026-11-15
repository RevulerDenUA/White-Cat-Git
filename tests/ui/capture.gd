extends SceneTree
## Uses the real renderer and real UI scenes; --headless cannot produce the preview images.

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var args := OS.get_cmdline_user_args()
	var output := "/tmp/white-cat-menu.png"
	var screen := "menu"
	var size := Vector2i(1600, 900)
	var scroll := 0
	for arg: String in args:
		if arg.begins_with("--output="):
			output = arg.trim_prefix("--output=")
		elif arg.begins_with("--screen="):
			screen = arg.trim_prefix("--screen=")
		elif arg.begins_with("--size="):
			var dimensions := arg.trim_prefix("--size=").split("x")
			size = Vector2i(int(dimensions[0]), int(dimensions[1]))
		elif arg.begins_with("--scroll="):
			scroll = int(arg.trim_prefix("--scroll="))
	DisplayServer.window_set_size(size)
	# A real offscreen viewport prevents a tiling compositor from changing the capture size.
	var viewport := SubViewport.new()
	viewport.size = size
	var aspect := float(size.x) / float(size.y)
	viewport.size_2d_override = Vector2i(roundi(1080 * aspect), 1080) if aspect >= 1920.0 / 1080.0 else Vector2i(1920, roundi(1920.0 / aspect))
	viewport.size_2d_override_stretch = true
	viewport.own_world_3d = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	viewport.msaa_3d = SubViewport.MSAA_2X
	root.add_child(viewport)
	var menu: Node3D = load("res://Scene/UI/MainMenu.tscn").instantiate()
	viewport.add_child(menu)
	await create_timer(2.0).timeout
	if screen.begins_with("settings"):
		menu.open_settings()
		await create_timer(0.4).timeout
		var category := int(screen.trim_prefix("settings"))
		menu.settings._show_category(category)
		menu.settings._tabs[category].grab_focus()
		await process_frame
		await process_frame
		menu.settings._scroll.scroll_vertical = scroll
	elif screen == "sources":
		menu.open_settings()
		menu.settings._show_category(6)
		await create_timer(0.4).timeout
		for link: Node in menu.settings._content.find_children("*", "LinkButton", true, false):
			if link.text == "Повний аудит і перелік 50 зображень":
				link.pressed.emit()
				break
		await create_timer(0.2).timeout
	elif screen == "license":
		menu.open_settings()
		menu.settings._show_category(6)
		menu.settings._open_license("Onest", FileAccess.get_file_as_string("res://ASS/UI/Fonts/Onest-OFL.txt"))
		await create_timer(0.4).timeout
	elif screen.begins_with("launch"):
		menu.start_game()
		await create_timer(float(screen.trim_prefix("launch"))).timeout
	elif screen == "transition":
		DirAccess.make_dir_recursive_absolute(output)
		for frame: int in 150:
			if frame == 30:
				menu.start_game()
			await process_frame
			await RenderingServer.frame_post_draw
			viewport.get_texture().get_image().save_png(output.path_join("%04d.png" % frame))
		var target: String = menu.backdrop.config.get_value("scene", "game_scene")
		ResourceLoader.load_threaded_get(target)
		print("CAPTURE: 150 real transition frames in %s" % output)
		quit()
		return
	await RenderingServer.frame_post_draw
	DirAccess.make_dir_recursive_absolute(output.get_base_dir())
	var err := viewport.get_texture().get_image().save_png(output)
	print("CAPTURE: %s (%s)" % [output, error_string(err)])
	if screen.begins_with("launch"):
		# Drain the real background load before shutting down the engine mid-transition.
		var target: String = menu.backdrop.config.get_value("scene", "game_scene")
		ResourceLoader.load_threaded_get(target)
	quit(0 if err == OK else 1)
