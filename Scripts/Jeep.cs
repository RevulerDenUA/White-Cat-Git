using Godot;
using System;
using static Godot.GD;

public partial class Jeep : VehicleBody3D
{
    float steerInput;
    float gasInput;
    [Export] Label label;

    [Export] public Node3D ExitPoint;


    public override void _Process(double delta)
    {
    }

    public override void _PhysicsProcess(double delta)
    {

        if (GameData.InSpectatorMode)
        {
            return;
        }
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