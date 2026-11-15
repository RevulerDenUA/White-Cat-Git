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

	public float Perihelion; // Closest distance to the star
	public float Aphelion; // Farthest distance from the star
	public float Distance;
	public float BaseSpeed;
	public float Inclination;
	public float OrbitYaw;

	public float Radius;
	public float Gravity;

	public float Mass;
	public float Temperature;


}


public partial class SystemValues : Node3D
{
	// Allows other scripts to access the Inspector values on this node
	public static SystemValues Instance { get; private set; } = new SystemValues();

	public const float Gvalues = 6.67430f;


	[Export] public int SystemSeed = 12345;
	[Export] public int PlanetCount = 1;
	[Export] public float MeshBaseRadius = 1.0f;


	public void CalculateGravity(ref PlanetData dat)
	{
		float halfRadius = Mathf.Max(dat.Radius * 0.5f, 0.001f);
		dat.Gravity = SystemValues.Gvalues * (dat.Mass / (halfRadius * halfRadius));
	}


	public static int Getplanet(int seed)
	{
		
	RandomNumberGenerator rng = new RandomNumberGenerator();
	rng.Seed = (ulong)Mathf.Abs(seed);
	rng.Randi(); 
	int count = rng.RandiRange(1, 9);

	GD.Print(count);

	return count;
		
	}

	public static double WarpSpeed = 1.0;
	public static List<PlanetOrbit> Planets = new List<PlanetOrbit>();

	public override void _EnterTree()
	{
		Instance = this;
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
