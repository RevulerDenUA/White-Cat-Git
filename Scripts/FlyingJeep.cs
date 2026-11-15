using Godot;

public partial class FlyingJeep : CharacterBody3D
{
	[Export] public float RotorSpeed = 25f; // рад/с

	private Node3D _rotorUpper, _rotorLower;

	public override void _Ready()
	{
		_rotorUpper = FindChild("Rotor_Upper", true, false) as Node3D;
		_rotorLower = FindChild("Rotor_Lower", true, false) as Node3D;
	}

	public override void _Process(double delta)
	{
		// співвісні гвинти крутяться в різні боки
		float a = RotorSpeed * (float)delta;
		_rotorUpper?.RotateY(a);
		_rotorLower?.RotateY(-a);
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		bool controlled = GameData.CanDrive(this);

		float forward = 0f, turn = 0f, vertical = 0f;
		if (controlled)
		{
			if (ControlInput.IsPressed("forwardy")) forward += 1f;
			if (ControlInput.IsPressed("backwardy")) forward -= 1f;
			if (ControlInput.IsPressed("lefty")) turn += 1f;
			if (ControlInput.IsPressed("righty")) turn -= 1f;
			if (ControlInput.IsPressed("spacy")) vertical += 1f;
			if (ControlInput.IsPressed("flight_descend")) vertical -= 1f;
		}

		RotateY(turn * FlyJeepData.TurnSpeed * dt);
		
		Vector3 target = GlobalTransform.Basis.Z * forward * FlyJeepData.MaxSpeed
		                 + Vector3.Up * vertical * FlyJeepData.VerticalSpeed;
		Velocity = Velocity.MoveToward(target, FlyJeepData.Acceleration * dt);
		MoveAndSlide();
	}
}
