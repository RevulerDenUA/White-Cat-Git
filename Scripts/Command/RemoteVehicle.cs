using Godot;
using Godot.Collections;

/// Scene adapter: registration, camera identity and an optional recovery owner.
public partial class RemoteVehicle : Node
{
	[Signal] public delegate void RecoveryRequestedEventHandler();
	[Export] public string TargetId = "";
	[Export] public string DisplayName = "";
	[Export] public Node3D Vehicle;
	[Export] public Array<Camera3D> Cameras = new();
	[Export] public Array<Camera3D> ColorCameras = new();
	[Export] public bool RecoverOnRelease;
	[Export] public bool SupportsVerticalControl;
	[Export] public Array<Texture2D> IdentificationImages = new();

	public override void _Ready()
	{
		Vehicle ??= GetParent() as Node3D;
		AddToGroup("remote_vehicle");
		foreach (Node node in GetTree().GetNodesInGroup("vehicle_controller"))
			if (node is CameraControl controller && controller.GetViewport() == GetViewport())
				controller.register_vehicle(this);
	}

	public static bool IsLive(Node node) => GodotObject.IsInstanceValid(node)
		&& node.IsInsideTree() && !node.IsQueuedForDeletion();

	public Array<Camera3D> LiveCameras()
	{
		var result = new Array<Camera3D>();
		foreach (Camera3D camera in Cameras)
			if (IsLive(camera) && camera.GetViewport() == GetViewport()) result.Add(camera);
		return result;
	}

	public bool Owns(Camera3D camera) => LiveCameras().Contains(camera);
	public static string CameraId(Camera3D camera) => camera.Name.ToString().ToLowerInvariant();

	public void ReleaseControl()
	{
		if (RecoverOnRelease && IsLive(Vehicle))
			EmitSignal(SignalName.RecoveryRequested);
	}
}
