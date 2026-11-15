class_name MenuBackdrop
extends Node3D

var camera: Camera3D
var station: Node3D
var environment: Environment
var key_light: DirectionalLight3D
var config := ConfigFile.new()
var _base_camera := Vector3.ZERO
var _target := Vector3.ZERO
var _time := 0.0
var _launch := false
var _ring: Node3D
var _earth: MeshInstance3D
var using_preview := false
var launch_progress := 0.0
var _launch_start := Vector3.ZERO
var _launch_end := Vector3.ZERO
var _launch_control := Vector3.ZERO
var _attributes: CameraAttributesPractical

func _ready() -> void:
	config.load("res://Scene/UI/menu.cfg")
	environment = Environment.new()
	environment.background_mode = Environment.BG_SKY
	var sky_material := PanoramaSkyMaterial.new()
	sky_material.panorama = load("res://ASS/Sky/plain_starfield_1.png")
	sky_material.energy_multiplier = 0.28
	var sky := Sky.new()
	sky.sky_material = sky_material
	environment.sky = sky
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color("92adc7")
	environment.ambient_light_energy = 0.17
	environment.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	var world := WorldEnvironment.new()
	world.environment = environment
	add_child(world)
	key_light = DirectionalLight3D.new()
	key_light.rotation_degrees = Vector3(-32, -135, 0)
	key_light.light_color = Color("fff0ce")
	key_light.light_energy = 1.7
	add_child(key_light)
	var fill := DirectionalLight3D.new()
	fill.rotation_degrees = Vector3(-8, 30, 0)
	fill.light_color = Color("7dadd2")
	fill.light_energy = 0.32
	add_child(fill)
	station = StationPreview.instantiate_visual()
	using_preview = station != null
	if station == null:
		var path: String = config.get_value("scene", "station_scene")
		station = (load(path) as PackedScene).instantiate() as Node3D
	if using_preview:
		var bounds := _visual_bounds(station, Transform3D.IDENTITY)
		var longest := maxf(bounds.size.x, maxf(bounds.size.y, bounds.size.z))
		var visual := station
		station = Node3D.new()
		station.add_child(visual)
		visual.position -= bounds.get_center()
		if longest > 0.001:
			station.scale = Vector3.ONE * 12.76 / longest
	add_child(station)
	station.position = config.get_value("scene", "station_position")
	station.rotation_degrees = config.get_value("scene", "station_rotation")
	_ring = station.find_child("Habitat_Ring", true, false) as Node3D
	_add_earth()
	_add_sun()
	camera = Camera3D.new()
	_base_camera = config.get_value("scene", "camera_position")
	_target = config.get_value("scene", "camera_target")
	camera.position = _base_camera
	camera.fov = config.get_value("scene", "camera_fov")
	camera.far = 1000
	add_child(camera)
	_attributes = CameraAttributesPractical.new()
	_attributes.dof_blur_amount = 0.12
	camera.attributes = _attributes
	camera.look_at(_target)
	camera.make_current()
	UserSettings.changed.connect(apply_settings)
	apply_settings()
	get_viewport().size_changed.connect(_frame_camera)
	_frame_camera()

func _frame_camera() -> void:
	if _launch:
		return
	var extent := get_viewport().get_visible_rect().size
	var aspect := extent.x / maxf(extent.y, 1)
	var portrait := aspect < 1.3
	_base_camera = config.get_value("scene", "camera_position")
	_target = config.get_value("scene", "camera_target")
	if portrait:
		_base_camera = Vector3(station.position.x, station.position.y + 4.0, 56.0)
		_target = station.position + Vector3(0, -10.0, 0)
	elif aspect < 1.7:
		_base_camera.z *= (1920.0 / 1080.0) / aspect
	camera.position = _base_camera
	camera.look_at(_target)

func _visual_bounds(node: Node3D, parent_transform: Transform3D) -> AABB:
	var transform := parent_transform * node.transform
	var bounds := AABB()
	if node is MeshInstance3D:
		bounds = transform * node.get_aabb()
	for child: Node in node.get_children():
		if child is Node3D:
			bounds = bounds.merge(_visual_bounds(child, transform))
	return bounds

func _add_sun() -> void:
	var glow := MeshInstance3D.new()
	var quad := QuadMesh.new()
	quad.size = Vector2(32, 32)
	glow.mesh = quad
	glow.position = Vector3(5, 15, -100)
	var material := ShaderMaterial.new()
	material.shader = load("res://shader/menu_sun.gdshader")
	glow.material_override = material
	add_child(glow)

func _add_earth() -> void:
	_earth = MeshInstance3D.new()
	var sphere := SphereMesh.new()
	var radius: float = config.get_value("scene", "earth_radius")
	sphere.radius = radius
	sphere.height = radius * 2
	sphere.radial_segments = 128
	sphere.rings = 64
	_earth.mesh = sphere
	_earth.position = config.get_value("scene", "earth_position")
	_earth.rotation_degrees = Vector3(12, 115, -18)
	var material := ShaderMaterial.new()
	material.shader = load("res://shader/menu_earth.gdshader")
	material.set_shader_parameter("earth_texture", load("res://ASS/UI/Textures/earth_nasa.jpg"))
	_earth.material_override = material
	add_child(_earth)
	var atmosphere := MeshInstance3D.new()
	atmosphere.mesh = sphere
	atmosphere.scale = Vector3.ONE * 1.008
	var air := ShaderMaterial.new()
	air.shader = load("res://shader/menu_atmosphere.gdshader")
	atmosphere.material_override = air
	_earth.add_child(atmosphere)

func apply_settings() -> void:
	environment.glow_enabled = UserSettings.values["glow"] and RenderingServer.get_current_rendering_method() != "gl_compatibility"
	environment.glow_intensity = 0.35
	key_light.shadow_enabled = UserSettings.values["shadows"]

func _process(delta: float) -> void:
	if _launch:
		var t := launch_progress
		camera.position = _launch_start.bezier_interpolate(_launch_control, _launch_control, _launch_end, t)
		camera.look_at(_target.lerp(station.position, smoothstep(0.0, 0.75, t)))
		camera.fov = lerpf(float(config.get_value("scene", "camera_fov")), 38.0, t)
		_attributes.dof_blur_far_distance = camera.position.distance_to(station.position) + 7.0
		_attributes.dof_blur_amount = lerpf(0.0, 0.18, smoothstep(0.1, 0.7, t))
		return
	if UserSettings.values["reduced_motion"]:
		return
	_time += delta
	var phase: float = _time * TAU / float(config.get_value("scene", "orbit_period", 90.0))
	var amount: float = config.get_value("scene", "orbit_amount", 0.6)
	camera.position = _base_camera + Vector3(sin(phase) * amount, sin(phase * 0.7) * amount * 0.35, 0)
	camera.look_at(_target)
	if _ring != null:
		_ring.rotate_z(delta * 0.008)
	_earth.rotate_y(delta * 0.001)

func launch(seconds: float) -> void:
	_launch = true
	_launch_start = camera.position
	_launch_end = station.position + station.basis * Vector3(0, 0, 13.5)
	_launch_control = _launch_start.lerp(_launch_end, 0.5) + Vector3(-1.0, 1.5, 2.0)
	_attributes.dof_blur_far_enabled = RenderingServer.get_current_rendering_method() != "gl_compatibility"
	_attributes.dof_blur_far_transition = 15.0
	var tween := create_tween().set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	tween.tween_property(self, "launch_progress", 1.0, seconds)

func reset_camera() -> void:
	_launch = false
	launch_progress = 0.0
	_attributes.dof_blur_far_enabled = false
	camera.position = _base_camera
	camera.fov = config.get_value("scene", "camera_fov")
	camera.look_at(_target)
