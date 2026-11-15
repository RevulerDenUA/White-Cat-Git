using Godot;
using System;

public partial class SteeringWheel : MeshInstance3D
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _PhysicsProcess(double delta)
	{
		Vector3 Wheelangle = RotationDegrees;

		if (Input.IsActionPressed("lefty"))
		{
			Wheelangle.Z = Wheelangle.Z - 1f;
			if (Wheelangle.Z < -20)
			{
				Wheelangle.Z = -20;
			}
		}
		else if (Input.IsActionPressed("righty"))
		{
			Wheelangle.Z = Wheelangle.Z + 1f;
			if (Wheelangle.Z > 20)
			{
				Wheelangle.Z = 20;
			}
		}

		RotationDegrees = Wheelangle;
	}
}
