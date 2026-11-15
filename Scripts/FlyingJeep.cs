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
		bool controlled = !GameData.InSpectatorMode && GameData.ActiveVehicle == this;

		float forward = 0f, turn = 0f, vertical = 0f;
		if (controlled)
		{
			if (Input.IsActionPressed("forwardy")) forward += 1f;
			if (Input.IsActionPressed("backwardy")) forward -= 1f;
			if (Input.IsActionPressed("lefty")) turn += 1f;
			if (Input.IsActionPressed("righty")) turn -= 1f;
			if (Input.IsActionPressed("spacy")) vertical += 1f;
			if (Input.IsActionPressed("flight_descend")) vertical -= 1f;
		}

		RotateY(turn * FlyJeepData.TurnSpeed * dt);
		
		Vector3 target = GlobalTransform.Basis.Z * forward * FlyJeepData.MaxSpeed
		                 + Vector3.Up * vertical * FlyJeepData.VerticalSpeed;
		Velocity = Velocity.MoveToward(target, FlyJeepData.Acceleration * dt);
		MoveAndSlide();
	}
}
