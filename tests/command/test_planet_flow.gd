extends SceneTree
## Шатл → телескоп → висадка → повернення. Правила: газова планета — тільки Інжінюїті;
## шатл не летить на іншу планету, поки апарат зовні; апарат у зоні стикування — «в шатлі».
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

func key(code: int) -> void:
	for pressed: bool in [true, false]:
		var event := InputEventKey.new()
		event.physical_keycode = code
		event.keycode = code
		event.pressed = pressed
		Input.parse_input_event(event)
		await frames(1)

func open_shuttle_scene(path: String) -> Node:
	if current_scene == null or current_scene.scene_file_path != path:
		change_scene_to_file(path)
		await frames(6)
	return current_scene

func open_telescope() -> Control:
	var game := current_scene
	var controller: Node = game.get_node("CameraControl")
	var room: Node = game.get_node("CommandRoom")
	controller.open_terminal()
	await frames()
	room.execute_command("telescope")
	await frames(8)
	return room.get_telescope()

## A/D до планети потрібного типу; повертає false, якщо не знайшли за коло
func select_type(tel: Control, type: String, skip_current := false) -> bool:
	for index: int in 12:
		if tel.TargetType == type and not tel.TargetIsMoon and not (skip_current and tel.TargetIsCurrent):
			return true
		await key(KEY_D)
	return false

func wait_scene(path: String) -> bool:
	for index: int in 60:
		if current_scene != null and current_scene.scene_file_path == path:
			await frames(10)
			return true
		await frames(1)
	return false

func _run() -> void:
	await open_shuttle_scene("res://Scene/A.tscn")
	# Телескоп будується заздалегідь, прихованим
	var preloaded: Control = current_scene.get_node("CommandRoom").find_child("Telescope", true, false)
	check(preloaded != null and not preloaded.visible, "telescope is not preloaded hidden")
	var tel := await open_telescope()
	check(tel != null, "telescope did not open")
	check(tel == preloaded, "telescope was rebuilt instead of reusing the preloaded one")
	for index: int in 300:
		if tel.IsLoaded:
			break
		await frames(1)
	check(tel.IsLoaded and is_equal_approx(tel.Progress, 1.0), "telescope did not finish loading")
	if tel == null:
		_finish()
		return
	check(tel.Target != null, "telescope has no initial target")

	# A / D перемикають планети
	var first: int = tel.TargetSeed
	await key(KEY_D)
	check(tel.TargetSeed != first, "D did not change the planet")
	await key(KEY_A)
	check(tel.TargetSeed == first, "A did not return to the previous planet")

	# Газова планета: марсохід заборонено, Інжінюїті можна
	check(await select_type(tel, "GasGigant"), "no gas giant in the system")
	await key(KEY_1)
	check(tel.Vehicle == "rover", "key 1 did not choose the rover")
	check(tel.CanLaunchTo("rover") != "", "rover allowed on a gas giant")
	await key(KEY_ENTER)
	await frames(6)
	check(current_scene.scene_file_path == "res://Scene/A.tscn", "rover launched to a gas giant")
	check(tel.Message != "", "no explanation for the rejected launch")
	await key(KEY_2)
	check(tel.CanLaunchTo("ingenuity") == "", "Ingenuity rejected on a gas giant: " + tel.CanLaunchTo("ingenuity"))
	await key(KEY_ENTER)
	check(await wait_scene("res://Scene/WorldGen.tscn"), "launch did not open the planet scene")

	# На газовій планеті: керуємо Інжінюїті, марсохода немає, жил немає
	var world := current_scene
	var controller: Node = world.get_node("CameraControl")
	check(world.Planet == 1, "terrain is not gas on a gas giant (Planet=%s)" % world.Planet)
	check(controller.snapshot()["context"] == "vehicle", "no direct control after landing")
	check(controller.snapshot()["target_id"] == "ingenuity", "wrong vehicle connected")
	check(world.get_node_or_null("Rover") == null, "rover is outside on a gas giant")
	var drone: Node3D = world.get_node("FlyingJeep")
	check(world.get_viewport().get_camera_3d() != null and drone.is_ancestor_of(world.get_viewport().get_camera_3d()), "drone camera is not current")
	check(_veins(world) == 0, "veins generated on a gas giant")

	# Апарат стартує всередині шатла; HUD показує планету й фото апарата
	var pad: Node3D = world.get_node("ShuttlePad")
	check(pad.IsDocked(drone), "drone did not start inside the shuttle")
	var session: Node = world.get_node("PlanetSession")
	await frames(4)
	check("Планета" in session.CardTitle or "Місяць" in session.CardTitle, "planet card has no title")
	check("жил немає" in session.CardFacts, "gas planet card does not explain the missing veins")
	check(session.PhotoVisible, "vehicle photo is not shown")
	# вилітаємо назовні й повертаємося в шатл
	drone.global_position = pad.global_position + Vector3(0, 4, 60)
	await frames(6)
	check(not pad.IsDocked(drone), "drone far away is still docked")
	drone.global_position = pad.global_position + Vector3(0, 1.5, 0)
	await frames(6)
	check(pad.IsDocked(drone), "drone in the hull is not docked")
	await key(KEY_ESCAPE)
	check(await wait_scene("res://Scene/A.tscn"), "Esc did not return to the shuttle")

	# Кам'яна планета, марсохід
	tel = await open_telescope()
	check(tel.VehicleState("ingenuity") == "inside", "docked drone is not inside")
	check(await select_type(tel, "Rocky", true), "no other rocky planet")
	var rocky_seed: int = tel.TargetSeed
	await key(KEY_1)
	check(tel.CanLaunchTo("rover") == "", "rover rejected on a rocky planet: " + tel.CanLaunchTo("rover"))
	await key(KEY_ENTER)
	check(await wait_scene("res://Scene/WorldGen.tscn"), "rocky launch failed")
	world = current_scene
	controller = world.get_node("CameraControl")
	check(world.Planet == 0, "terrain is not rocky")
	check(controller.snapshot()["target_id"] == "rover", "rover not connected")
	check(world.get_node_or_null("FlyingJeep") == null, "drone should stay in the shuttle")
	check(_veins(world) > 0, "no veins on a rocky planet")
	var rover: Node3D = world.get_node("Rover")
	var ground: float = world.GetHeightAt(rover.global_position.x, rover.global_position.z)
	check(rover.global_position.y > ground - 0.5, "rover spawned under the terrain")
	check(world.get_node("ShuttlePad").IsDocked(rover), "rover did not start inside the shuttle")
	check("жили поруч" in world.get_node("PlanetSession").CardFacts, "rocky planet card does not list veins")
	# виводимо марсохід за рампу і лишаємо там
	var outside: Node3D = world.get_node("ShuttlePad")
	rover.global_position = outside.global_position + Vector3(0, 0, 70)
	rover.global_position.y = world.GetHeightAt(rover.global_position.x, rover.global_position.z) + 1.6
	await frames(30)
	var parked := rover.global_position
	check(not world.get_node("ShuttlePad").IsDocked(rover), "rover outside is still docked")
	await key(KEY_ESCAPE)
	check(await wait_scene("res://Scene/A.tscn"), "Esc from the rocky planet failed")

	# Марсохід зовні: інша планета заборонена, ця сама — можна
	tel = await open_telescope()
	check(tel.VehicleState("rover") == "outside", "rover left outside is reported inside")
	check(await select_type(tel, "Rocky", true), "no third planet to test the rule")
	await key(KEY_2)
	check(tel.CanLaunchTo("ingenuity") != "", "shuttle may leave with the rover outside")
	await key(KEY_ENTER)
	await frames(6)
	check(current_scene.scene_file_path == "res://Scene/A.tscn", "shuttle left with the rover outside")
	for index: int in 12:
		if tel.TargetSeed == rocky_seed and not tel.TargetIsMoon:
			break
		await key(KEY_D)
	check(tel.TargetIsCurrent, "could not reselect the shuttle planet")
	check(tel.CanLaunchTo("ingenuity") == "", "drone rejected on the shuttle planet")
	await key(KEY_ENTER)
	check(await wait_scene("res://Scene/WorldGen.tscn"), "relaunch on the same planet failed")
	world = current_scene
	rover = world.get_node_or_null("Rover")
	check(rover != null, "parked rover disappeared")
	if rover != null:
		check(rover.global_position.distance_to(parked) < 2.0, "parked rover moved: %s vs %s" % [rover.global_position, parked])
	check(world.get_node("CameraControl").snapshot()["target_id"] == "ingenuity", "drone not connected on relaunch")
	_finish()

func _veins(world: Node) -> int:
	var count := 0
	for child: Node in world.get_node("OreGenerator").get_children():
		if child.name.begins_with("Vein_"):
			count += 1
	for child: Node in world.get_children():
		if child.name.begins_with("Vein_"):
			count += 1
	return count

func _finish() -> void:
	print("planet flow: %d checks, %d failures" % [checks, failures])
	quit(1 if failures > 0 else 0)
