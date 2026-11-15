extends SceneTree
# Справжня їзда: C — сісти в джип, далі тримаємо клавіші за сценарієм
var jeep; var t := 0.0; var maxtilt := 0.0; var flips := 0; var upside := false; var maxspd := 0.0
var plan := []
func _initialize():
	var scene = load("res://Scene/A.tscn").instantiate()
	root.add_child(scene)
	jeep = scene.get_node("CharacterBody3D")
	# сценарій: [час_від, час_до, дії]
	var mode = OS.get_cmdline_user_args()[0]
	match mode:
		"zigzag": plan = [[1.0, 30.0, ["forwardy"]], [3.0, 6.0, ["lefty"]], [6.0, 9.0, ["righty"]], [9.0, 12.0, ["lefty"]], [12.0, 15.0, ["righty"]], [15.0, 18.0, ["lefty"]], [18.0, 21.0, ["righty"]]]
		"circle_l": plan = [[1.0, 30.0, ["forwardy", "lefty"]]]
		"circle_r": plan = [[1.0, 30.0, ["forwardy", "righty"]]]
		"rev": plan = [[1.0, 30.0, ["backwardy", "lefty"]]]
		"snap": plan = [[1.0, 30.0, ["forwardy"]], [4.0, 4.6, ["lefty"]], [4.6, 5.2, ["righty"]], [5.2, 5.8, ["lefty"]], [5.8, 6.4, ["righty"]]]
func _physics_process(delta) -> bool:
	t += delta
	if t > 0.5 and t < 0.5 + delta * 1.5: Input.action_press("CameraChange")
	elif t > 0.6 and t < 0.6 + delta * 1.5: Input.action_release("CameraChange")
	for a in ["forwardy", "backwardy", "lefty", "righty"]:
		var on = false
		for p in plan:
			if t >= p[0] and t < p[1] and a in p[2]: on = true
		if on and not Input.is_action_pressed(a): Input.action_press(a)
		elif not on and Input.is_action_pressed(a): Input.action_release(a)
	# тільки поки на платформі (не падає з краю)
	if jeep.global_position.y > 0.5 and t > 1.0:
		var tilt = rad_to_deg(jeep.global_transform.basis.y.angle_to(Vector3.UP))
		maxtilt = max(maxtilt, tilt); maxspd = max(maxspd, jeep.linear_velocity.length())
		if tilt > 90 and not upside: flips += 1
		upside = tilt > 90
	if t > 22.0:
		print("RESULT %s tilt_max=%.0f flips=%d speed_max=%.1f" % [OS.get_cmdline_user_args()[0], maxtilt, flips, maxspd]); quit()
	return false
