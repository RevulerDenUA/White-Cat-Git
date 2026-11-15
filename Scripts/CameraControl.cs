using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using GDictionary = Godot.Collections.Dictionary;

/// Authoritative room/terminal/piloting context, shared with the physics scripts via GameData.
public partial class CameraControl : Node
{
	[Signal] public delegate void SessionChangedEventHandler();
	[Signal] public delegate void RegistryChangedEventHandler();
	[Signal] public delegate void DiagnosticEventHandler(string message);

	[Export] public Node3D RoomView;
	[Export] public Camera3D SpecCamera;
	[Export] public ColorRect Filter;
	[Export(PropertyHint.Layers3DRender)] public uint RoomLayerMask = 1u << 19;
	[ExportGroup("Signal effect (not radio telemetry)")]
	[Export] public float NoiseStartHeight = 3f;
	[Export] public float NoiseFullHeight = 40f;
	[Export] public float NoiseStartSpeed = 8f;
	[Export] public float NoiseFullSpeed = 30f;
	[Export] public float MaxNoise = 0.95f;

	private readonly System.Collections.Generic.Dictionary<string, RemoteVehicle> _targets = new(StringComparer.OrdinalIgnoreCase);
	private readonly System.Collections.Generic.Dictionary<RemoteVehicle, List<(Node Node, Action Callback)>> _watchers = new();
	private RemoteVehicle _active;
	private Camera3D _camera { get => GameData.ActiveCamera; set => GameData.ActiveCamera = value; }
	private Camera3D _roomCamera;
	private ShaderMaterial _filterMat;
	private Node _settings;
	private Callable _settingsCallback;
	private static readonly string[] MovementActions = {
		"forwardy", "backwardy", "lefty", "righty", "spacy", "flight_descend",
		"spectator_forward", "spectator_backward", "spectator_left", "spectator_right", "spectator_up", "spectator_down"
	};

	public override void _EnterTree() => AddToGroup("vehicle_controller");

	public override void _Ready()
	{
		GameData.ActiveVehicle = null;
		_camera = null;
		GameData.InputArmed = false;
		GameData.CameraIndex = -1;
		GameData.Context = ControlContext.Room;
		ControlInput.Reset();
		_filterMat = Filter?.Material as ShaderMaterial;
		if (RemoteVehicle.IsLive(RoomView) && RoomView.HasMethod("get_room_camera"))
			_roomCamera = RoomView.Call("get_room_camera").AsGodotObject() as Camera3D;
		_settings = GetNodeOrNull<Node>("/root/UserSettings");
		_settingsCallback = Callable.From(OnSettingsChanged);
		_settings?.Connect("changed", _settingsCallback);
		foreach (Node node in GetTree().GetNodesInGroup("remote_vehicle"))
			if (node is RemoteVehicle target) register_vehicle(target);
		return_to_room();
	}

	public uint get_room_layer() => RoomLayerMask;
	public Camera3D get_active_camera() => RemoteVehicle.IsLive(_camera) ? _camera : null;
	public Material get_signal_material() => Filter?.Material;
	private bool Setting(string key) => _settings != null && _settings.Call("get_value", key).AsBool();

	public bool register_vehicle(RemoteVehicle target)
	{
		if (!RemoteVehicle.IsLive(target) || target.GetViewport() != GetViewport()
			|| !RemoteVehicle.IsLive(target.Vehicle) || !ValidId(target.TargetId)) return false;
		if (_targets.TryGetValue(target.TargetId, out RemoteVehicle existing))
		{
			if (existing == target) return true;
			GD.PushError($"Duplicate remote vehicle id: {target.TargetId}");
			return false;
		}
		var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (Camera3D camera in target.LiveCameras())
		{
			if (!target.Vehicle.IsAncestorOf(camera) || !ValidId(RemoteVehicle.CameraId(camera))
				|| !ids.Add(RemoteVehicle.CameraId(camera)))
			{
				GD.PushError($"Invalid camera registration: {target.TargetId}/{camera.Name}");
				return false;
			}
			camera.CullMask &= ~RoomLayerMask;
		}
		_targets.Add(target.TargetId, target);
		_watchers.Add(target, new List<(Node, Action)>());
		Watch(target, target, () => RemoveTarget(target));
		Watch(target, target.Vehicle, () => RemoveTarget(target));
		foreach (Camera3D camera in target.LiveCameras())
			Watch(target, camera, () => RemoveCamera(target, camera));
		EmitSignal(SignalName.RegistryChanged);
		return true;
	}

	private static bool ValidId(string id)
	{
		if (string.IsNullOrEmpty(id) || id.Length > 48) return false;
		foreach (char c in id)
			if (!char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-') return false;
		return true;
	}

	private void Watch(RemoteVehicle owner, Node node, Action action)
	{
		node.TreeExiting += action;
		_watchers[owner].Add((node, action));
	}

	private void RemoveTarget(RemoteVehicle target)
	{
		if (!_targets.TryGetValue(target.TargetId, out RemoteVehicle registered) || registered != target) return;
		_targets.Remove(target.TargetId);
		Unwatch(target);
		if (_active == target)
		{
			_active = null;
			return_to_room();
			EmitSignal(SignalName.Diagnostic, "Апарат від'єднано від сцени.");
		}
		EmitSignal(SignalName.RegistryChanged);
	}

	private void RemoveCamera(RemoteVehicle target, Camera3D camera)
	{
		target.Cameras.Remove(camera);
		target.ColorCameras.Remove(camera);
		if (_camera == camera)
		{
			return_to_room();
			EmitSignal(SignalName.Diagnostic, "Камеру від'єднано від сцени.");
		}
		EmitSignal(SignalName.RegistryChanged);
	}

	private void Unwatch(RemoteVehicle target)
	{
		if (!_watchers.Remove(target, out var list)) return;
		foreach (var watch in list)
			if (GodotObject.IsInstanceValid(watch.Node)) watch.Node.TreeExiting -= watch.Callback;
	}

	public Godot.Collections.Array<GDictionary> catalog()
	{
		var result = new Godot.Collections.Array<GDictionary>();
		foreach (var pair in _targets)
		{
			RemoteVehicle target = pair.Value;
			if (!RemoteVehicle.IsLive(target) || !RemoteVehicle.IsLive(target.Vehicle)) continue;
			var cameras = new Godot.Collections.Array<GDictionary>();
			foreach (Camera3D camera in target.LiveCameras())
				cameras.Add(new GDictionary { ["id"] = RemoteVehicle.CameraId(camera), ["name"] = camera.Name.ToString(),
					["address"] = target.TargetId + "/" + RemoteVehicle.CameraId(camera) });
			result.Add(new GDictionary { ["id"] = target.TargetId, ["name"] = target.DisplayName,
				["cameras"] = cameras, ["connected"] = _active == target && GameData.Context == ControlContext.Vehicle });
		}
		return result;
	}

	public GDictionary snapshot() => new() {
		["context"] = GameData.Context.ToString().ToLowerInvariant(),
		["target_id"] = RemoteVehicle.IsLive(_active) ? _active.TargetId : "",
		["target_name"] = RemoteVehicle.IsLive(_active) ? _active.DisplayName : "",
		["camera_id"] = RemoteVehicle.IsLive(_camera) && _active != null ? RemoteVehicle.CameraId(_camera) : "",
		["camera_name"] = RemoteVehicle.IsLive(_camera) && _active != null ? _camera.Name.ToString() : "",
		["input_armed"] = GameData.InputArmed,
		["vertical_control"] = RemoteVehicle.IsLive(_active) && _active.SupportsVerticalControl,
		["look_control"] = _camera is VehicleCamera camera && camera.EnableLook,
		["identification_images"] = RemoteVehicle.IsLive(_active) ? _active.IdentificationImages : new Godot.Collections.Array<Texture2D>(),
	};

	// Returns a message on failure. A rejected address never alters the active session.
	public string connect_target(string address)
	{
		if (string.IsNullOrEmpty(address) || address.Length > 100) return "Вкажіть назву апарата з help.";
		string[] parts = address.Split('/');
		if (parts.Length > 2 || !ValidId(parts[0]) || (parts.Length == 2 && !ValidId(parts[1])))
			return "Формат: connect апарат або connect апарат/камера.";
		if (!_targets.TryGetValue(parts[0], out RemoteVehicle target) || !RemoteVehicle.IsLive(target)
			|| !RemoteVehicle.IsLive(target.Vehicle)) return "Апарат не знайдено. Доступні назви — у help.";
		var cameras = target.LiveCameras();
		if (cameras.Count == 0) return "В апарата немає підключеної камери.";
		Camera3D chosen = cameras[0];
		if (parts.Length == 2)
		{
			chosen = null;
			foreach (Camera3D camera in cameras)
				if (RemoteVehicle.CameraId(camera).Equals(parts[1], StringComparison.OrdinalIgnoreCase)) chosen = camera;
			if (chosen == null) return "Камеру не знайдено. Доступні адреси — у help.";
		}
		if (_active != target) ReleaseTarget();
		_active = target;
		_camera = chosen;
		GameData.ActiveVehicle = target.Vehicle;
		GameData.Context = ControlContext.Vehicle;
		GameData.CameraIndex = cameras.IndexOf(chosen);
		GameData.InputArmed = false;
		ControlInput.Reset();
		// The room camera stays current; RoomView renders this source on the monitor.
		if (RemoteVehicle.IsLive(_roomCamera)) _roomCamera.MakeCurrent();
		Input.MouseMode = Input.MouseModeEnum.Visible;
		EmitSignal(SignalName.SessionChanged);
		return "";
	}

	public void open_terminal()
	{
		if (GameData.Context != ControlContext.Room) return;
		GameData.Context = ControlContext.Terminal;
		GameData.InputArmed = false;
		EmitSignal(SignalName.SessionChanged);
	}

	public void return_to_room()
	{
		ReleaseTarget();
		_camera = null;
		GameData.ActiveVehicle = null;
		GameData.CameraIndex = -1;
		GameData.Context = ControlContext.Room;
		GameData.InputArmed = false;
		if (RemoteVehicle.IsLive(_roomCamera)) _roomCamera.MakeCurrent();
		else if (RemoteVehicle.IsLive(SpecCamera)) SpecCamera.MakeCurrent();
		Input.MouseMode = Input.MouseModeEnum.Visible;
		EmitSignal(SignalName.SessionChanged);
		UpdateFilter();
	}

	private void ReleaseTarget()
	{
		ControlInput.Reset();
		RemoteVehicle previous = _active;
		_active = null;
		GameData.ActiveVehicle = null;
		GameData.InputArmed = false;
		if (RemoteVehicle.IsLive(previous)) previous.ReleaseControl();
	}

	private void NextCamera()
	{
		if (!RemoteVehicle.IsLive(_active)) return;
		var cameras = _active.LiveCameras();
		if (cameras.Count == 0) { return_to_room(); return; }
		int next = (cameras.IndexOf(_camera) + 1) % cameras.Count;
		_camera = cameras[next];
		GameData.CameraIndex = next;
		EmitSignal(SignalName.SessionChanged);
	}

	public override void _Input(InputEvent input)
	{
		if (input.IsActionPressed("ui_cancel") && !input.IsEcho())
		{
			GetViewport().SetInputAsHandled();
			return_to_room();
		}
	}

	public override void _UnhandledInput(InputEvent input)
	{
		if (GetTree().Paused || input.IsEcho()) return;
		if (GameData.Context == ControlContext.Vehicle && input.IsActionPressed("CameraChange"))
		{
			NextCamera();
			GetViewport().SetInputAsHandled();
		}
		else if (GameData.Context != ControlContext.Terminal && input.IsActionPressed("SpectatorMode") && Setting("developer_mode"))
		{
			if (GameData.Context == ControlContext.Spectator) return_to_room();
			else if (RemoteVehicle.IsLive(SpecCamera))
			{
				Transform3D pose = GetViewport().GetCamera3D()?.GlobalTransform ?? SpecCamera.GlobalTransform;
				ReleaseTarget();
				_camera = null;
				GameData.Context = ControlContext.Spectator;
				GameData.CameraIndex = -1;
				if (SpecCamera is SpectatorMode spectator) spectator.Activate(pose);
				SpecCamera.MakeCurrent();
				Input.MouseMode = Input.MouseModeEnum.Captured;
				EmitSignal(SignalName.SessionChanged);
			}
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (GameData.InputArmed || GetTree().Paused || (DisplayServer.GetName() != "headless" && !GetWindow().HasFocus())
			|| (GameData.Context != ControlContext.Vehicle && GameData.Context != ControlContext.Spectator)) return;
		foreach (string action in MovementActions)
			if (InputMap.HasAction(action) && Input.IsActionPressed(action)) return;
		ControlInput.Reset();
		GameData.InputArmed = true;
		EmitSignal(SignalName.SessionChanged);
	}

	public override void _Notification(int what)
	{
		if (what != NotificationApplicationFocusOut || !GameData.InputArmed) return;
		GameData.InputArmed = false;
		ControlInput.Reset();
		EmitSignal(SignalName.SessionChanged);
	}

	private void OnSettingsChanged()
	{
		if (GameData.Context == ControlContext.Spectator && !Setting("developer_mode")) return_to_room();
	}

	public override void _Process(double delta) => UpdateFilter();

	private void UpdateFilter()
	{
		if (!GodotObject.IsInstanceValid(Filter)) return;
		Filter.Visible = false; // The same effect is composited inside the physical display.
		if (_filterMat == null || !RemoteVehicle.IsLive(_active)) return;
		Node3D vehicle = _active.Vehicle;
		float speed = vehicle switch { RigidBody3D rb => rb.LinearVelocity.Length(), CharacterBody3D cb => cb.Velocity.Length(), _ => 0f };
		float h = Mathf.Clamp((vehicle.GlobalPosition.Y - NoiseStartHeight) / Mathf.Max(NoiseFullHeight - NoiseStartHeight, 0.001f), 0f, 1f);
		float s = Mathf.Clamp((speed - NoiseStartSpeed) / Mathf.Max(NoiseFullSpeed - NoiseStartSpeed, 0.001f), 0f, 1f);
		_filterMat.SetShaderParameter("signal_quality", 1f - (1f - (1f - h) * (1f - s)) * Mathf.Clamp(MaxNoise, 0f, 1f));
		_filterMat.SetShaderParameter("color_mode", _active.ColorCameras.Contains(_camera));
	}

	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(_settings) && _settings.IsConnected("changed", _settingsCallback))
			_settings.Disconnect("changed", _settingsCallback);
		foreach (RemoteVehicle target in new List<RemoteVehicle>(_watchers.Keys)) Unwatch(target);
		_targets.Clear();
		ControlInput.Reset();
		GameData.ActiveVehicle = null;
		_camera = null;
		GameData.InputArmed = false;
		GameData.Context = ControlContext.Room;
		GameData.CameraIndex = -1;
	}
}
