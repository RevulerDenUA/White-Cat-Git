extends SceneTree

var problems := 0
var inspected := 0

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	for size: Vector2i in [Vector2i(1920, 1080), Vector2i(2560, 1080), Vector2i(1920, 1440), Vector2i(1920, 4160)]:
		var viewport := SubViewport.new()
		viewport.size = size
		root.add_child(viewport)
		var menu: Node3D = load("res://Scene/UI/MainMenu.tscn").instantiate()
		viewport.add_child(menu)
		await _frames()
		_check_tree(menu.ui, viewport, size)
		menu.open_settings()
		await _frames()
		for category: int in menu.settings.CATEGORIES.size():
			menu.settings._show_category(category)
			await _frames()
			_check_tree(menu.settings, viewport, size)
		menu.settings._open_license("Onest", FileAccess.get_file_as_string("res://ASS/UI/Fonts/Onest-OFL.txt"))
		await _frames()
		for child: Node in menu.settings.get_children():
			if child is CreditsLicenseDialog:
				_check_tree(child, viewport, size)
		viewport.queue_free()
		await _frames()
	print("LAYOUT: %d visible controls inspected; %d failures" % [inspected, problems])
	quit(1 if problems else 0)

func _frames() -> void:
	for i: int in 4:
		await process_frame

func _check_tree(ui: Control, viewport: Viewport, size: Vector2i) -> void:
	if ui.has_method("_show_category") and ui._scroll.size.y < 200:
		problems += 1
		push_error("Settings content collapsed at %s: %s" % [size, ui._scroll.size])
	if viewport.gui_get_focus_owner() == null:
		problems += 1
		push_error("No focused control at %s" % size)
	var screen := Rect2(Vector2.ZERO, Vector2(size)).grow(2)
	for child: Node in ui.find_children("*", "Control", true, false):
		var control := child as Control
		if not control.is_visible_in_tree():
			continue
		var ancestor := control.get_parent()
		var inside_scroll := false
		while ancestor != null and ancestor != ui:
			if ancestor is ScrollContainer:
				inside_scroll = true
				break
			ancestor = ancestor.get_parent()
		if inside_scroll:
			continue
		inspected += 1
		if not screen.encloses(control.get_global_rect()):
			problems += 1
			push_error("%s outside %s: %s" % [control.get_path(), size, control.get_global_rect()])
