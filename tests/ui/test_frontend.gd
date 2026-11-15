extends SceneTree

var failures := 0
var checks := 0
var settings: Node
var preview: Node

func _initialize() -> void:
	_run.call_deferred()

func check(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		push_error(message)

func frames(count: int = 3) -> void:
	for i: int in count:
		await process_frame

func _run() -> void:
	settings = root.get_node("UserSettings")
	preview = root.get_node("StationPreview")
	var baseline: Dictionary = settings.values.duplicate(true)
	var baseline_keys: Dictionary = settings.bindings.duplicate(true)
	var bad: Dictionary = settings.DEFAULTS.duplicate(true)
	bad["master_volume"] = -5.0
	bad["render_scale"] = NAN
	bad["resolution"] = Vector2i(-1, 99999)
	bad["fps_limit"] = 9
	var clean: Dictionary = settings.validated(bad)
	check(clean["master_volume"] == 0.0 and clean["render_scale"] == 1.0, "Invalid numeric settings were not bounded")
	check(clean["resolution"] == Vector2i(960, 4320) and clean["fps_limit"] == 60, "Invalid display settings were not bounded")
	check(settings.binding_conflict("forwardy", KEY_W, baseline_keys) == "", "Separate control contexts must share movement keys")
	check(settings.binding_conflict("forwardy", KEY_C, baseline_keys) == "CameraChange", "Global camera bindings must conflict with movement")
	var custom_keys: Dictionary = baseline_keys.duplicate(true)
	custom_keys["forwardy"] = KEY_Z
	check(settings.save_values(baseline, custom_keys) == OK, "Settings could not be saved")
	var disk := ConfigFile.new()
	check(disk.load(settings.PATH) == OK and disk.get_value("bindings", "forwardy") == KEY_Z, "Bindings did not round-trip through disk")
	var bound: InputEventKey = InputMap.action_get_events("forwardy")[0]
	check(bound.physical_keycode == KEY_Z, "Saved physical key did not reach InputMap")
	custom_keys["forwardy"] = KEY_C
	check(settings.save_values(baseline, custom_keys) == ERR_INVALID_DATA, "Conflicting binding was accepted")
	check(settings.bindings["forwardy"] == KEY_Z, "Invalid settings changed live state")
	settings.save_values(baseline, baseline_keys)
	var menu: Node3D = load("res://Scene/UI/MainMenu.tscn").instantiate()
	root.add_child(menu)
	current_scene = menu
	await frames()
	check(root.gui_get_focus_owner() == menu.start_button, "Main menu has no default focus")
	menu.open_settings()
	await frames()
	var panel: Control = menu.settings
	for index: int in panel.CATEGORIES.size():
		panel._show_category(index)
		await frames()
		check(root.gui_get_focus_owner() != null, "Settings category %d lost keyboard focus" % index)
	panel._show_category(panel.CATEGORIES.size() - 1)
	await frames()
	var credits := panel._content.get_child(-1) as CreditsView
	check(credits != null and not panel._is_dirty(), "Credits tab changed settings or did not open")
	panel.draft["sfx_volume"] = 0.31
	var pending: Dictionary = panel.draft.duplicate(true)
	var source_link: LinkButton
	for link: Node in credits.find_children("*", "LinkButton", true, false):
		if link.text == "Повний аудит і перелік 50 зображень":
			source_link = link
	check(source_link != null, "Complete scientific source inventory has no offline action")
	if source_link != null:
		source_link.grab_focus()
		source_link.pressed.emit()
		await frames()
		var document_dialog: CreditsLicenseDialog
		for child: Node in panel.get_children():
			if child is CreditsLicenseDialog:
				document_dialog = child
		check(document_dialog != null and panel._pending_modal, "Offline source inventory did not open")
		if document_dialog != null:
			check(not document_dialog.reader.editable and "25 ОРИГІНАЛЬНИХ ФОТО" in document_dialog.reader.text and "PIA24096" in document_dialog.reader.text and "NASA/JPL-Caltech/LANL/CNES/IRAP" in document_dialog.reader.text, "Original image credits are missing from the offline inventory")
			document_dialog.finish()
			await frames()
			check(root.gui_get_focus_owner() == source_link and not panel._pending_modal and panel.draft == pending, "Closing the source reader lost focus or pending settings")
	var license_link: LinkButton
	for link: Node in credits.find_children("*", "LinkButton", true, false):
		if link.text == "Текст ліцензії":
			license_link = link
			break
	check(license_link != null, "Font licence has no reachable action")
	if license_link != null:
		license_link.grab_focus()
		license_link.pressed.emit()
		await frames()
		var license_dialog: CreditsLicenseDialog
		for child: Node in panel.get_children():
			if child is CreditsLicenseDialog:
				license_dialog = child
		check(license_dialog != null and panel._pending_modal, "Licence dialog did not open as a modal")
		if license_dialog != null:
			check(license_dialog.reader.text == FileAccess.get_file_as_string("res://ASS/UI/Fonts/Onest-OFL.txt") and not license_dialog.reader.editable, "Full font licence was modified or is editable")
			check(license_dialog.is_ancestor_of(root.gui_get_focus_owner()), "Licence dialog did not trap keyboard focus")
			var escape := InputEventAction.new()
			escape.action = "ui_cancel"
			escape.pressed = true
			license_dialog._unhandled_input(escape)
			await frames()
			check(is_instance_valid(panel) and not panel._pending_modal and root.gui_get_focus_owner() == license_link, "Closing licence lost focus or closed settings")
			check(panel.draft == pending and panel._is_dirty(), "Reading credits discarded pending settings")
	check("Godot Engine" in CreditsView.component_licenses() and "License:" in CreditsView.component_licenses(), "Engine component credits are unavailable")
	panel.draft = baseline.duplicate(true)
	panel._update_dirty()
	panel._show_category(0)
	await frames()
	var sliders := panel.find_children("*", "HSlider", true, false)
	(sliders[0] as HSlider).value = 0.32
	check(is_equal_approx(AudioServer.get_bus_volume_linear(0), 0.32), "Audio preview did not reach the actual mixer")
	panel._discard_and_close()
	await frames()
	check(is_equal_approx(AudioServer.get_bus_volume_linear(0), baseline["master_volume"]), "Cancel did not restore audio")
	check(root.gui_get_focus_owner() == menu.settings_button, "Closing settings did not restore menu focus")
	menu.open_settings()
	await frames()
	panel = menu.settings
	panel._show_category(3)
	await frames()
	var capture_button := WhiteCatTheme.button("Test binding")
	panel._content.add_child(capture_button)
	panel._begin_rebind("forwardy", "Рух уперед", capture_button)
	await frames()
	check(panel._key_dialog.is_ancestor_of(root.gui_get_focus_owner()), "Rebinding modal did not trap focus")
	var event := InputEventKey.new()
	event.pressed = true
	event.physical_keycode = KEY_C
	panel._input(event)
	check(panel._waiting == "forwardy", "Conflicting key closed the capture dialog")
	event.physical_keycode = KEY_Z
	panel._input(event)
	await frames()
	check(panel.draft_bindings["forwardy"] == KEY_Z and panel._waiting == "", "New key was not captured")
	check(root.gui_get_focus_owner() != null, "Closing capture dialog lost focus")
	panel.draft["window_mode"] = 1
	panel.call_deferred("apply_changes")
	await frames()
	var modal: MenuDialog
	for child: Node in panel.get_children():
		if child is MenuDialog:
			modal = child
	check(modal != null, "Display changes skipped confirmation")
	if modal != null:
		modal.finish(1)
	await frames()
	check(settings.values["window_mode"] == baseline["window_mode"], "Rejected display mode reached saved state")
	check(panel.draft["window_mode"] == baseline["window_mode"], "Rejected display mode did not roll back the form")
	panel.draft["window_mode"] = 1
	panel.call_deferred("apply_changes")
	await create_timer(15.5).timeout
	check(not panel._pending_modal and panel.draft["window_mode"] == baseline["window_mode"], "Display settings did not automatically revert after the confirmation timeout")
	panel._discard_and_close()
	await frames()
	var provider: Node = load("res://tests/ui/resource_probe.gd").new()
	root.add_child(provider)
	check(settings.register_resource_provider(provider), "Resource provider was not accepted")
	var dev: Dictionary = baseline.duplicate(true)
	dev["developer_mode"] = true
	dev["infinite_resources"] = true
	settings.save_values(dev, baseline_keys)
	check(provider.infinite, "Infinite resources did not reach the registered resource system")
	dev["developer_mode"] = false
	settings.save_values(dev, baseline_keys)
	check(not provider.infinite, "Disabling developer mode left resource override active")
	provider.queue_free()
	await frames()
	check(not is_instance_valid(settings.resource_provider), "Resource provider was retained after scene exit")
	var source: Node3D = load("res://ASS/Station/Space_Station.glb").instantiate()
	var source_mesh := source.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
	var painted := source_mesh.get_active_material(0) as StandardMaterial3D
	var original_color := painted.albedo_color
	painted.albedo_color = Color(0.2, 0.4, 0.7)
	var added := MeshInstance3D.new()
	added.name = "PlayerAddedModule"
	added.mesh = BoxMesh.new()
	added.position = Vector3(4, 1, 0)
	source.add_child(added)
	check(preview.capture(source) == OK, "Modified station preview could not be saved")
	painted.albedo_color = original_color
	var restored: Node3D = preview.instantiate_visual()
	check(restored != null and restored.find_child("PlayerAddedModule", true, false) != null, "Player station modification did not survive preview capture")
	if restored != null:
		check(restored.get_script() == null and restored.find_children("*", "PhysicsBody3D", true, false).is_empty(), "Preview contained gameplay code or physics")
		var meshes := restored.find_children("*", "MeshInstance3D", true, false)
		check(not meshes.is_empty() and (meshes[0] as MeshInstance3D).get_active_material(0) != null, "Preview lost the station material")
		check(((meshes[0] as MeshInstance3D).get_active_material(0) as StandardMaterial3D).albedo_color.is_equal_approx(Color(0.2, 0.4, 0.7)), "Paint changes were lost when an imported project material was reloaded")
		restored.free()
	source.free()
	DirAccess.remove_absolute(preview.PATH)
	settings.save_values(baseline, baseline_keys)
	menu.start_game()
	await create_timer(4.5).timeout
	check(current_scene != null and current_scene.scene_file_path == "res://Scene/A.tscn", "Start did not finish the cinematic transition into the actual game")
	DirAccess.remove_absolute(settings.PATH)
	print("FRONTEND: %d checks; %d failures" % [checks, failures])
	quit(1 if failures else 0)
