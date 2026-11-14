using Godot;
using System;

public partial class CameraControl : Node
{
    [Export] public Camera3D JeepCamera1;
    [Export] public Camera3D JeepCamera2;
    [Export] public Camera3D JeepCamera3;

    [Export] public Camera3D SpecCamera;
    public override void _Ready()
    {
        SpecCamera.MakeCurrent();
    }

    private void NextCamera()
    {
        if (GameData.CameraIndex == 0)
        {
            JeepCamera1.MakeCurrent();
            GameData.CameraIndex++;
        }
        else if (GameData.CameraIndex == 1)
        {
            JeepCamera2.MakeCurrent();
            GameData.CameraIndex++;
        }
        else if (GameData.CameraIndex == 2)
        {
            JeepCamera3.MakeCurrent();
            GameData.CameraIndex = 0;
        }
    }


    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("CameraChange"))
        {
            NextCamera();
            GameData.InSpectatorMode = false;
        }
        else if (Input.IsActionJustReleased("SpectatorMode"))
        {
            SpecCamera.MakeCurrent();
            GameData.InSpectatorMode = true;
        }
        
    }
}