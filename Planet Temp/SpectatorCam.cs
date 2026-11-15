 
using Godot;

public partial class SpectatorCam : Camera3D
{
	[Export] public float MoveSpeed = 10.0f;
	[Export] public float MouseSensitivity = 0.1f;

	private float _yaw = 0.0f;
	private float _pitch = 0.0f;
	private float _speedScale = 1.0f;

	public override async void _Ready()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		Input.MouseMode = Input.MouseModeEnum.Captured;

		Camera3D currentCamera = GetViewport().GetCamera3D();

		if (currentCamera != null)
			Position = currentCamera.Position;
	}

	public override void _Input(InputEvent @event)
	{
		if (!Current) return;

		if (@event is InputEventMouseMotion mouseMotion)
		{
			var settings = GetNodeOrNull<Node>("/root/UserSettings");
			float sensitivity = settings == null ? 1f : (float)settings.Call("get_value", "mouse_sensitivity").AsDouble();
			bool invert = settings != null && settings.Call("get_value", "invert_y").AsBool();
			_yaw -= mouseMotion.Relative.X * MouseSensitivity * sensitivity;
			_pitch -= mouseMotion.Relative.Y * MouseSensitivity * sensitivity * (invert ? -1f : 1f);

			_pitch = Mathf.Clamp(_pitch, -89.0f, 89.0f);
		}

		if (@event is InputEventMouseButton mouseButton)
		{
			if (mouseButton.Pressed)
			{
				if (mouseButton.ButtonIndex == MouseButton.WheelUp)
					_speedScale -= 0.1f;

				if (mouseButton.ButtonIndex == MouseButton.WheelDown)
					_speedScale += 0.1f;
			}

			_speedScale = Mathf.Max(_speedScale, 0.1f);

			GD.Print(_speedScale);
		}

		if (@event.IsActionPressed("ui_cancel"))
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
	}

	public override void _Process(double delta)
	{
		if (!Current) return;

		Vector3 direction = Vector3.Zero;

		Vector3 forward = -GlobalTransform.Basis.Z;
		Vector3 right = GlobalTransform.Basis.X;
		Vector3 up = GlobalTransform.Basis.Y;

		if (Input.IsActionPressed("spectator_forward"))
			direction += forward;

		if (Input.IsActionPressed("spectator_backward"))
			direction -= forward;

		if (Input.IsActionPressed("spectator_left"))
			direction -= right;

		if (Input.IsActionPressed("spectator_right"))
			direction += right;

		if (Input.IsActionPressed("spectator_up"))
			direction += up;

		if (Input.IsActionPressed("spectator_down"))
			direction -= up;

		Basis yawBasis = new Basis(
			Vector3.Up,
			Mathf.DegToRad(_yaw)
		);

		Basis pitchBasis = new Basis(
			Vector3.Right,
			Mathf.DegToRad(_pitch)
		);

		Transform3D cameraTransform = Transform;
		cameraTransform.Basis = (yawBasis * pitchBasis).Orthonormalized();
		Transform = cameraTransform;

		direction = direction.Normalized()
			* MoveSpeed
			* _speedScale
			* (float)delta;

		Translate(direction);
	}
}
 
