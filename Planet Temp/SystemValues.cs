using Godot;
using System;
using System.Collections.Generic;

public struct ParentPlanetData
{
	public string Type;
	public int Order;
	public int PlanetSeed;
	public int NoiseSeed;

	public float Radius;
	public float Squish;
	public float AxialTilt;
	public float rotationspeed;
	public float AtmospherePressure;

	public Color SkyDark;
	public Color SkyMid;
	public Color SkyLight;
	public Color SkyFog;
}

public struct PlanetData
{
	public bool IsMoon;
	public int MoonCount;
	public ParentPlanetData ParentPlanet;

	public string Type;
	public int Order;
	public int PlanetSeed;
	public int NoiseSeed;

	public float Perihelion;
	public float Aphelion;
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

	public float AtmospherePressure;
	public string AtmoTemplate;
	public string MainGas;

	public float Hydrogen;
	public float Helium;
	public float Nitrogen;
	public float Oxygen;
	public float CarbonDioxide;
	public float Methane;
	public float Ammonia;
	public float SulfurDioxide;

	public string CoreTemplate;
	public string MainSolid;
	public float Iron;
	public float Silicates;
	public float WaterIce;
	public float Carbon;
	public float Sulfur;
}

public partial class SystemValues : Node3D
{
	public static SystemValues Instance { get; private set; } = new SystemValues();

	public const float Gvalues = 6.67430f;

[Export] public int SystemSeed = 12345;
	[Export] public int PlanetCount = 1;
	[Export] public float MeshBaseRadius = 1.0f;
	[Export] public float StarRadius = 60.0f;

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

	public static void SpawnMoons(PlanetOrbit parentPlanet, int moonCount)
	{
		if (parentPlanet == null || moonCount <= 0) return;

		Node systemRoot = parentPlanet.GetParent();
		if (systemRoot == null) return;

		for (int i = 0; i < moonCount; i++)
		{
			PlanetOrbit moon = (PlanetOrbit)parentPlanet.Duplicate();
			moon._isClone = true;
			moon._isMoon = true;
			moon.OrbitIndex = i;
			moon.ParentOrbit = parentPlanet;

			systemRoot.CallDeferred(Node.MethodName.AddChild, moon);
		}
	}
}
