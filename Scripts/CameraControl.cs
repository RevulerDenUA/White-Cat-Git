using Godot;
using System;

public partial class CameraControl : Node
{
	[Export] public Camera3D JeepCamera1;
	[Export] public Camera3D JeepCamera2;
	[Export] public Camera3D JeepCamera3;
	[Export] public Camera3D SpecCamera;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		SpecCamera.MakeCurrent();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
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
}
