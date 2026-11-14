using Godot;
using System;
using static Godot.GD;

public partial class Jeep : VehicleBody3D
{
	float steerInput;
	float gasInput;
	[Export] Label label;
	[Export] public Camera3D JeepCamera1;
	[Export] public Camera3D JeepCamera2;
	[Export] public Camera3D JeepCamera3;
	[Export] public Node3D ExitPoint;


	public override void _Ready()
	{
		JeepCamera1.MakeCurrent();
	}


	public override void _Process(double delta)
	{
		if (Input.IsActionJustPressed("Camera1"))
		{
			JeepCamera1.MakeCurrent();
		}
		else if (Input.IsActionJustPressed("Camera2"))
		{
			JeepCamera2.MakeCurrent();
		}
		else if (Input.IsActionJustPressed("Camer3"))
		{
			JeepCamera3.MakeCurrent();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		steerInput = 0.0f; 

		if (Input.IsActionPressed("lefty"))
		{
			steerInput = 1.0f;
		}
		else if (Input.IsActionPressed("righty"))
		{
			steerInput = -1.0f; 
		}

		float targetSteer = steerInput * JeepData.MaxSteerAngle;
		Steering = Mathf.MoveToward(Steering, targetSteer, JeepData.SteerSpeed * (float)delta);


		gasInput = 0.0f; 

		if (Input.IsActionPressed("forwardy"))
		{
			gasInput = JeepData.Speed; 
		}
		else if (Input.IsActionPressed("backwardy"))
		{
			gasInput = -JeepData.Speed; 
		}

		EngineForce = gasInput * JeepData.MaxEngineForce;
		
	}
}
