extends Node
## Stores visual geometry only. Physics, scripts and gameplay state never enter a menu preview.
const PATH := "user://menu/station_preview.scn"
const TEMP_PATH := "user://menu/station_preview.tmp.scn"
var _config := ConfigFile.new()

func _ready() -> void:
	_config.load("res://Scene/UI/menu.cfg")

func capture(station: Node3D) -> Error:
	if not is_instance_valid(station):
		return ERR_INVALID_PARAMETER
	var budget := [int(_config.get_value("preview", "max_nodes", 1024))]
	var visual := _copy_visual(station, budget)
	if visual == null or budget[0] < 0:
		if visual != null:
			visual.free()
		return ERR_OUT_OF_MEMORY
	visual.transform = Transform3D.IDENTITY
	_set_owners(visual, visual)
	var packed := PackedScene.new()
	var result := packed.pack(visual)
	visual.free()
	if result != OK:
		return result
	DirAccess.make_dir_recursive_absolute("user://menu")
	result = ResourceSaver.save(packed, TEMP_PATH)
	if result != OK:
		return result
	if _file_length(TEMP_PATH) > int(_config.get_value("preview", "max_file_bytes", 33554432)):
		DirAccess.remove_absolute(TEMP_PATH)
		return ERR_OUT_OF_MEMORY
	return DirAccess.rename_absolute(TEMP_PATH, PATH)

func capture_from_tree() -> void:
	var group: String = _config.get_value("preview", "group", "menu_preview_station")
	var station := get_tree().get_first_node_in_group(group) as Node3D
	if station != null:
		var err := capture(station)
		if err != OK:
			push_warning("Station menu preview could not be saved: %s" % error_string(err))

func instantiate_visual() -> Node3D:
	if not FileAccess.file_exists(PATH):
		return null
	if _file_length(PATH) > int(_config.get_value("preview", "max_file_bytes", 33554432)):
		return null
	var packed := ResourceLoader.load(PATH, "PackedScene", ResourceLoader.CACHE_MODE_IGNORE) as PackedScene
	if packed == null:
		return null
	var visual := packed.instantiate() as Node3D
	if visual == null:
		return null
	# Also validate loaded previews: they are render-only trees written by capture().
	var budget := [int(_config.get_value("preview", "max_nodes", 1024))]
	if not _validate(visual, budget):
		visual.free()
		return null
	return visual

func _file_length(path: String) -> int:
	var file := FileAccess.open(path, FileAccess.READ)
	return file.get_length() if file != null else 0x7fffffff

func _copy_visual(source: Node, budget: Array) -> Node3D:
	budget[0] -= 1
	if budget[0] < 0:
		return null
	if source is Node3D and not source.visible:
		return null
	var copy: Node3D
	if source is MeshInstance3D:
		var mesh := source as MeshInstance3D
		if not mesh.visible or mesh.mesh == null:
			return null
		var item := MeshInstance3D.new()
		item.mesh = mesh.mesh.duplicate() as Mesh
		for i: int in mesh.mesh.get_surface_count():
			var material := mesh.get_active_material(i)
			if material != null:
				item.set_surface_override_material(i, material.duplicate() as Material)
		copy = item
	elif source is Node3D:
		copy = Node3D.new()
	else:
		return null
	copy.name = source.name
	copy.transform = (source as Node3D).transform
	for child: Node in source.get_children():
		var visual := _copy_visual(child, budget)
		if visual != null:
			copy.add_child(visual)
	return copy

func _set_owners(node: Node, root: Node) -> void:
	for child: Node in node.get_children():
		child.owner = root
		_set_owners(child, root)

func _validate(node: Node, budget: Array) -> bool:
	budget[0] -= 1
	if budget[0] < 0 or not node is Node3D or node.get_script() != null:
		return false
	if not (node is MeshInstance3D or node.get_class() == "Node3D"):
		return false
	for child: Node in node.get_children():
		if not _validate(child, budget):
			return false
	return true

func return_to_menu() -> Error:
	capture_from_tree()
	get_tree().paused = false
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	return get_tree().change_scene_to_file("res://Scene/UI/MainMenu.tscn")
