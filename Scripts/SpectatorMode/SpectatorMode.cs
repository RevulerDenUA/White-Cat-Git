
using Godot;

public partial class SpectatorMode : Camera3D
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
		if (@event is InputEventMouseMotion mouseMotion)
		{
			_yaw -= mouseMotion.Relative.X * MouseSensitivity;
			_pitch -= mouseMotion.Relative.Y * MouseSensitivity;

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
		Vector3 direction = Vector3.Zero;

		Vector3 forward = -GlobalTransform.Basis.Z;
		Vector3 right = GlobalTransform.Basis.X;
		Vector3 up = GlobalTransform.Basis.Y;

		if (Input.IsKeyPressed(Key.W))
			direction += forward;

		if (Input.IsKeyPressed(Key.S))
			direction -= forward;

		if (Input.IsKeyPressed(Key.A))
			direction -= right;

		if (Input.IsKeyPressed(Key.D))
			direction += right;

		if (Input.IsKeyPressed(Key.E))
			direction += up;

		if (Input.IsKeyPressed(Key.Q))
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
