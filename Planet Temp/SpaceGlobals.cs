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

            // [1] Rocky Mass & Gravity via SystemValues
            float halfR = d.Radius * 0.5f;
            d.Mass = (halfR * halfR) * rng.RandfRange(0.55f, 1.70f);
            SystemValues.Instance.CalculateGravity(ref d);

            d.Temperature = (300.0f / Mathf.Sqrt(d.Distance)) + rng.RandfRange(-20.0f, 20.0f);
            d.rotationspeed = rng.RandfRange(0.5f, 2.0f);
        }
        else if (type == "GasGigant")
        {
            d.Perihelion = baseDist + rng.RandfRange(-1.5f, 1.5f);
            d.Aphelion = d.Perihelion + rng.RandfRange(1.0f, 6.0f);
            d.Distance = (d.Aphelion + d.Perihelion) * 0.5f;

            d.Inclination = rng.RandfRange(-7.0f, 7.0f);
            d.OrbitYaw = rng.RandfRange(0.0f, 360.0f);

            d.Radius = rng.RandfRange(4.0f, 9.0f);

            // [2] Gas Giant Mass & Gravity via SystemValues
            float halfR = d.Radius * 0.5f;
            d.Mass = (halfR * halfR) * rng.RandfRange(1.80f, 3.75f);
            SystemValues.Instance.CalculateGravity(ref d);

            d.Temperature = (300.0f / Mathf.Sqrt(d.Distance)) + rng.RandfRange(-40.0f, 10.0f);
            d.rotationspeed = rng.RandfRange(1.5f, 4.0f);
        }

        d.BaseSpeed = (6.0f / Mathf.Sqrt(Mathf.Max(d.Distance, 0.1f))) * rng.RandfRange(0.9f, 1.1f);

        return d;
    }
}