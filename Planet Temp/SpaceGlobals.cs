using Godot;

public static class SpaceGlobals
{
    public static string manageroforder(int seed, int n, int x)
    {
        RandomNumberGenerator rng = new RandomNumberGenerator();
        rng.Seed = (ulong)Mathf.Abs(seed);

        int split = rng.RandiRange(1, Mathf.Max(1, n - 1));
        bool ordering = rng.RandiRange(0, 1) == 1;

        if (ordering)
            return (x >= split) ? "GasGigant" : "Rocky";
        else
            return (x < split) ? "GasGigant" : "Rocky";
    }

    public static PlanetData GeneratePlanet(int systemSeed, int order, string type)
    {
        PlanetData d = new PlanetData();
        d.Type = type;
        d.Order = order;
        d.PlanetSeed = systemSeed + (order * 1000);

        RandomNumberGenerator rng = new RandomNumberGenerator();
        rng.Seed = (ulong)Mathf.Abs(d.PlanetSeed);

        d.NoiseSeed = (int)rng.Randi();

        float baseDist = 8.0f + (order * 10.0f);

if (type == "Rocky")
        {
            d.Perihelion = baseDist + rng.RandfRange(-1.5f, 1.5f);
            d.Aphelion = d.Perihelion + rng.RandfRange(1.0f, 6.0f);
            d.Distance = (d.Aphelion + d.Perihelion) * 0.5f;

            d.Inclination = rng.RandfRange(-7.0f, 7.0f);
            d.OrbitYaw = rng.RandfRange(0.0f, 360.0f);

            d.Radius = rng.RandfRange(0.4f, 0.9f);

            float halfR = d.Radius * 0.5f;
            d.Mass = (halfR * halfR) * rng.RandfRange(0.55f, 1.70f);
            SystemValues.Instance.CalculateGravity(ref d);

            d.Temperature = (300.0f / Mathf.Sqrt(d.Distance)) + rng.RandfRange(-20.0f, 20.0f);
            d.rotationspeed = rng.RandfRange(0.5f, 2.0f);

            // [1] Rocky Approximation: 0.15% to 0.6% squish (matches Earth 0.0034 & Mars 0.0059)
            d.Squish = 1.0f - (d.rotationspeed * 0.003f);
            d.AxialTilt = rng.RandfRange(-28.0f, 28.0f);
        }
        else if (type == "GasGigant")
        {
            d.Perihelion = baseDist + rng.RandfRange(-1.5f, 1.5f);
            d.Aphelion = d.Perihelion + rng.RandfRange(1.0f, 6.0f);
            d.Distance = (d.Aphelion + d.Perihelion) * 0.5f;

            d.Inclination = rng.RandfRange(-7.0f, 7.0f);
            d.OrbitYaw = rng.RandfRange(0.0f, 360.0f);

            d.Radius = rng.RandfRange(4.0f, 9.0f);

            float halfR = d.Radius * 0.5f;
            d.Mass = (halfR * halfR) * rng.RandfRange(1.80f, 3.75f);
            SystemValues.Instance.CalculateGravity(ref d);

            d.Temperature = (300.0f / Mathf.Sqrt(d.Distance)) + rng.RandfRange(-40.0f, 10.0f);
            d.rotationspeed = rng.RandfRange(1.5f, 4.0f);

            // [2] Gas Giant Approximation: 3.7% to 10% squish (matches Uranus 0.023 to Saturn 0.098)
            d.Squish = 1.0f - (d.rotationspeed * 0.025f);
            d.AxialTilt = rng.RandfRange(-30.0f, 30.0f);
        }

		d.BaseSpeed = (6.0f / Mathf.Sqrt(Mathf.Max(d.Distance, 0.1f))) * rng.RandfRange(0.9f, 1.1f);

        //  Axial Tilt (random tilt up to 35 degrees, like Earth/Mars/Saturn)
        d.AxialTilt = rng.RandfRange(-35.0f, 35.0f);

        // Squish by Rotation Speed (faster spin = flatter poles, down to 0.75x height)
        d.Squish = Mathf.Clamp(1.0f - (d.rotationspeed * 0.06f), 0.75f, 1.0f);

        PlanetPackets.GenerateSkyColors(ref d);

        return d;
    }
}