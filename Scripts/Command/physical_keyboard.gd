class_name PhysicalCommandKeyboard
extends Node3D
## The supplied keyboard consists of 41 disconnected cuboids in two materials.
## Split those existing pieces at runtime; the imported asset stays unchanged.

var keys: Dictionary = {}
var _rests: Dictionary = {}
var _motions: Dictionary = {}
const TRAVEL := 0.008
const ROW_CODES := [
	[KEY_1, KEY_2, KEY_3, KEY_4, KEY_5, KEY_6, KEY_7, KEY_8, KEY_9, KEY_0],
	[KEY_Q, KEY_W, KEY_E, KEY_R, KEY_T, KEY_Y, KEY_U, KEY_I, KEY_O, KEY_P],
	[KEY_A, KEY_S, KEY_D, KEY_F, KEY_G, KEY_H, KEY_J, KEY_K, KEY_L],
	[KEY_SHIFT, KEY_Z, KEY_X, KEY_C, KEY_V, KEY_B, KEY_N, KEY_M, KEY_ENTER],
	[KEY_CTRL, KEY_SPACE, KEY_ALT],
]

func build(source: MeshInstance3D, layer_mask: int) -> void:
	var pieces: Array[MeshInstance3D] = []
	for surface: int in source.mesh.get_surface_count():
		var arrays := source.mesh.surface_get_arrays(surface)
		var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
		var normals: PackedVector3Array = arrays[Mesh.ARRAY_NORMAL]
		# Each authored cuboid has six independently normalled faces, 24 vertices.
		if vertices.size() % 24 != 0:
			push_warning("Command keyboard: changed source topology; keeping original geometry.")
			for piece: MeshInstance3D in pieces:
				piece.free()
			return
		var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
		for offset: int in range(0, vertices.size(), 24):
			var box := AABB(vertices[offset], Vector3.ZERO)
			for index: int in range(offset, offset + 24):
				box = box.expand(vertices[index])
			var center := box.get_center()
			var tool := SurfaceTool.new()
			tool.begin(Mesh.PRIMITIVE_TRIANGLES)
			tool.set_material(source.get_active_material(surface))
			for index: int in indices:
				if index >= offset and index < offset + 24:
					tool.set_normal(normals[index])
					tool.add_vertex(vertices[index] - center)
			var piece := MeshInstance3D.new()
			piece.mesh = tool.commit()
			piece.position = center
			piece.layers = layer_mask
			add_child(piece)
			pieces.append(piece)
	if pieces.size() != 41:
		for piece: MeshInstance3D in pieces:
			piece.free()
		push_warning("Command keyboard: expected the authored 41-key layout.")
		return
	pieces.sort_custom(func(a: MeshInstance3D, b: MeshInstance3D) -> bool:
		return a.position.z < b.position.z if absf(a.position.z - b.position.z) > 0.02 else a.position.x < b.position.x
	)
	var index := 0
	for row: Array in ROW_CODES:
		for code: int in row:
			var piece := pieces[index]
			piece.name = "Key_" + OS.get_keycode_string(code)
			keys[code] = piece
			_rests[code] = piece.position
			var legend := Label3D.new()
			legend.text = " " if code == KEY_SPACE else ("↵" if code == KEY_ENTER else OS.get_keycode_string(code))
			legend.font = WhiteCatTheme.FONT
			legend.font_size = 30 if code in [KEY_CTRL, KEY_SHIFT, KEY_ALT] else 42
			legend.pixel_size = 0.0005
			legend.outline_size = 0
			legend.modulate = Color("182733")
			legend.rotation.x = -PI * 0.5
			legend.position.y = 0.013
			legend.layers = layer_mask
			piece.add_child(legend)
			index += 1
	source.layers = 0

func handle_key(event: InputEvent) -> void:
	if event is InputEventKey and not event.echo:
		var code: int = event.physical_keycode if event.physical_keycode != 0 else event.keycode
		set_pressed(code, event.pressed)

func set_pressed(code: int, pressed: bool) -> void:
	if not keys.has(code):
		return
	if _motions.has(code) and is_instance_valid(_motions[code]):
		_motions[code].kill()
	var target: Vector3 = _rests[code] - Vector3.UP * TRAVEL if pressed else _rests[code]
	var duration := 0.0 if UserSettings.get_value("reduced_motion") else (0.035 if pressed else 0.065)
	var motion := create_tween()
	motion.tween_property(keys[code], "position", target, duration)
	_motions[code] = motion

func release_all() -> void:
	for code: int in keys:
		set_pressed(code, false)

func _notification(what: int) -> void:
	if what == NOTIFICATION_APPLICATION_FOCUS_OUT and is_inside_tree():
		release_all()
