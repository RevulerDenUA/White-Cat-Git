using Godot;
using System;
using System.Collections.Generic;

public struct PlanetData
{
    public string Type;
    public int Order;
    public int PlanetSeed;
    public int NoiseSeed;

    public float rotationspeed;

    public float Perihelion;
    public float Aphelion;
    public float Distance;
    public float BaseSpeed;
    public float Inclination;
    public float OrbitYaw;

    public float Radius;
    public float Gravity;
    public float Temperature;
}

public partial class SystemValues : Node
{
    public static SystemValues Instance { get; private set; } = new SystemValues();


    [Export] public int SystemSeed = 12345;
    [Export] public int PlanetCount = 7;
    [Export] public float MeshBaseRadius = 1.0f;

    public static double WarpSpeed = 1.0;
    public static List<PlanetOrbit> Planets = new List<PlanetOrbit>();

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
    }

    public override void _Process(double delta)
    {
    }

    public static void SetWarp(double multiplier)
    {
        WarpSpeed = Mathf.Max(0.0, multiplier);
    }

    public static void SpawnClones(PlanetOrbit origin, int extraCount)
    {
        if (origin == null) return;

        Node parent = origin.GetParent();
        if (parent == null) return;

        for (int i = 1; i <= extraCount; i++)
        {
            PlanetOrbit clone = (PlanetOrbit)origin.Duplicate();
            clone._isClone = true;
            clone.OrbitIndex = i;
            clone.StarNode = origin.StarNode;

            parent.CallDeferred(Node.MethodName.AddChild, clone);
        }
    }
}