using Godot;

public partial class SpectatorMode : Camera3D
{
	[Export] public float MoveSpeed = 10.0f;
	[Export] public float MouseSensitivity = 0.1f;
	private float _yaw;
	private float _pitch;
	private float _speedScale = 1f;

	public void Activate(Transform3D pose)
	{
		GlobalTransform = pose;
		_yaw = GlobalRotation.Y;
		_pitch = GlobalRotation.X;
	}

	public override void _UnhandledInput(InputEvent input)
	{
		if (!Current || GameData.Context != ControlContext.Spectator || GetTree().Paused) return;
		if (input is InputEventMouseMotion motion)
		{
			var settings = GetNodeOrNull<Node>("/root/UserSettings");
			float sensitivity = settings == null ? 1f : (float)settings.Call("get_value", "mouse_sensitivity").AsDouble();
			bool invert = settings != null && settings.Call("get_value", "invert_y").AsBool();
			_yaw -= Mathf.DegToRad(motion.Relative.X * MouseSensitivity * sensitivity);
			_pitch -= Mathf.DegToRad(motion.Relative.Y * MouseSensitivity * sensitivity * (invert ? -1f : 1f));
			_pitch = Mathf.Clamp(_pitch, Mathf.DegToRad(-89f), Mathf.DegToRad(89f));
			GlobalRotation = new Vector3(_pitch, _yaw, 0f);
		}
		if (input is InputEventMouseButton button && button.Pressed)
		{
			if (button.ButtonIndex == MouseButton.WheelUp) _speedScale = Mathf.Max(0.1f, _speedScale - 0.1f);
			if (button.ButtonIndex == MouseButton.WheelDown) _speedScale = Mathf.Min(10f, _speedScale + 0.1f);
		}
	}

	public override void _Process(double delta)
	{
		if (!Current || GameData.Context != ControlContext.Spectator || !GameData.InputArmed) return;
		var direction = Vector3.Zero;
		if (Input.IsActionPressed("spectator_forward")) direction -= GlobalBasis.Z;
		if (Input.IsActionPressed("spectator_backward")) direction += GlobalBasis.Z;
		if (Input.IsActionPressed("spectator_left")) direction -= GlobalBasis.X;
		if (Input.IsActionPressed("spectator_right")) direction += GlobalBasis.X;
		if (Input.IsActionPressed("spectator_up")) direction += Vector3.Up;
		if (Input.IsActionPressed("spectator_down")) direction -= Vector3.Up;
		GlobalPosition += direction.Normalized() * MoveSpeed * _speedScale * (float)delta;
	}
}
