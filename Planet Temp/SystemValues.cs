using Godot;
using System;
using System.Collections.Generic;

public struct PlanetData
{
	public string Type;
	public int Order;
	public int PlanetSeed;
	public int NoiseSeed;

	public float Perihelion; // Closest distance to the star
	public float Aphelion; // Farthest distance from the star
	public float Distance;
	public float BaseSpeed;
	public float Inclination;
	public float OrbitYaw;


	public float rotationspeed;
	public float Radius;
	public float Gravity;
	public float Mass;
	public float Temperature;
	public float AxialTilt; 
	public float Squish;  



	public Color SkyDark;
	public Color SkyMid;
	public Color SkyLight;
	public Color SkyFog;


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


public static void GenerateSkyColors(ref PlanetData d)
	{

		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Seed = (ulong)Mathf.Abs(d.NoiseSeed + 555);

		float baseHue = rng.Randf();

		d.SkyDark = Color.FromHsv(baseHue, rng.RandfRange(0.6f, 0.9f), rng.RandfRange(0.08f, 0.22f));

		float midHue = (baseHue + rng.RandfRange(-0.08f, 0.08f) + 1.0f) % 1.0f;
		d.SkyMid = Color.FromHsv(midHue, rng.RandfRange(0.4f, 0.75f), rng.RandfRange(0.35f, 0.55f));

		float lightHue = (baseHue + rng.RandfRange(-0.15f, 0.15f) + 1.0f) % 1.0f;
		d.SkyLight = Color.FromHsv(lightHue, rng.RandfRange(0.25f, 0.5f), rng.RandfRange(0.7f, 0.95f));

		d.SkyFog = Color.FromHsv(lightHue, rng.RandfRange(0.1f, 0.35f), rng.RandfRange(0.6f, 0.85f));
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
