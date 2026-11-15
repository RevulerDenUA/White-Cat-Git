using Godot;

public static class SpaceGlobals
{
    public static string manageroforder(int seed, int n, int x)
    {
        RandomNumberGenerator rng = new RandomNumberGenerator();
        rng.Seed = (ulong)Mathf.Abs(seed);

        int split = rng.RandiRange(1, Mathf.Max(1, n - 1));
        return (x >= split) ? "GasGigant" : "Rocky";
    }

    public static PlanetData GeneratePlanet(int systemSeed, int order, string type)
    {
        PlanetData d = new PlanetData();
        d.IsMoon = false;
        d.Type = type;
        d.Order = order;
        d.PlanetSeed = systemSeed + (order * 1000);

        RandomNumberGenerator rng = new RandomNumberGenerator();
        rng.Seed = (ulong)Mathf.Abs(d.PlanetSeed);

        d.NoiseSeed = (int)rng.Randi();


        float sunClearance = SystemValues.Instance.StarRadius * 2.2f;
        float baseDist = sunClearance + (65.0f * order) + (35.0f * Mathf.Pow(1.55f, order));

        if (type == "Rocky")
        {

            d.Perihelion = baseDist * rng.RandfRange(0.95f, 0.99f);
            d.Aphelion = baseDist * rng.RandfRange(1.01f, 1.07f);
            d.Distance = (d.Aphelion + d.Perihelion) * 0.5f;

            d.Inclination = rng.RandfRange(-3.5f, 3.5f);
            d.OrbitYaw = rng.RandfRange(0.0f, 360.0f);

            d.Radius = rng.RandfRange(0.4f, 0.9f);

            float halfR = d.Radius * 0.5f;
            d.Mass = (halfR * halfR) * rng.RandfRange(0.55f, 1.70f);
            SystemValues.Instance.CalculateGravity(ref d);

            d.Temperature = (3500.0f / Mathf.Sqrt(d.Distance)) + rng.RandfRange(-15.0f, 15.0f);


            d.rotationspeed = rng.RandfRange(0.02f, 0.08f);


            d.Squish = 1.0f - (d.rotationspeed * 0.075f);
            d.AxialTilt = rng.RandfRange(-28.0f, 28.0f);

            int moonRoll = rng.RandiRange(1, 10);
            if (moonRoll <= 6) d.MoonCount = 0;
            else if (moonRoll <= 8) d.MoonCount = 1;
            else if (moonRoll == 9) d.MoonCount = 2;
            else d.MoonCount = 3;
        }
        else if (type == "GasGigant")
        {
            d.Perihelion = baseDist * rng.RandfRange(0.94f, 0.98f);
            d.Aphelion = baseDist * rng.RandfRange(1.02f, 1.08f);
            d.Distance = (d.Aphelion + d.Perihelion) * 0.5f;

            d.Inclination = rng.RandfRange(-2.5f, 2.5f);
            d.OrbitYaw = rng.RandfRange(0.0f, 360.0f);

            d.Radius = rng.RandfRange(4.5f, 9.0f);

            float halfR = d.Radius * 0.5f;
            d.Mass = (halfR * halfR) * rng.RandfRange(1.80f, 3.75f);
            SystemValues.Instance.CalculateGravity(ref d);

            d.Temperature = (3500.0f / Mathf.Sqrt(d.Distance)) + rng.RandfRange(-30.0f, 10.0f);

            d.rotationspeed = rng.RandfRange(0.10f, 0.22f);


            d.Squish = 1.0f - (d.rotationspeed * 0.42f);
            d.AxialTilt = rng.RandfRange(-30.0f, 30.0f);


            d.MoonCount = rng.RandiRange(1, 20);
        }

        d.BaseSpeed = (180.0f / Mathf.Pow(Mathf.Max(d.Distance, 1.0f), 1.5f)) * rng.RandfRange(0.95f, 1.05f);

        PlanetPackets.GenerateSkyColors(ref d);
        PlanetPackets.GenerateChemicals(ref d);

        return d;
    }

    public static PlanetData GenerateMoon(PlanetData parent, int moonOrder)
    {
        PlanetData m = new PlanetData();
        m.IsMoon = true;
        m.Type = "Rocky";
        m.Order = moonOrder;
        m.MoonCount = 0;
        m.PlanetSeed = parent.PlanetSeed + ((moonOrder + 1) * 73);

        m.ParentPlanet = new ParentPlanetData
        {
            Type = parent.Type,
            Order = parent.Order,
            PlanetSeed = parent.PlanetSeed,
            NoiseSeed = parent.NoiseSeed,
            Radius = parent.Radius,
            Squish = parent.Squish,
            AxialTilt = parent.AxialTilt,
            rotationspeed = parent.rotationspeed,
            AtmospherePressure = parent.AtmospherePressure,
            SkyDark = parent.SkyDark,
            SkyMid = parent.SkyMid,
            SkyLight = parent.SkyLight,
            SkyFog = parent.SkyFog
        };

        RandomNumberGenerator rng = new RandomNumberGenerator();
        rng.Seed = (ulong)Mathf.Abs(m.PlanetSeed);
        m.NoiseSeed = (int)rng.Randi();

        m.Radius = Mathf.Min(rng.RandfRange(0.13f, 0.30f), parent.Radius * 0.33f);

        float clearance = (parent.Radius * 1.8f) + 0.8f;
        float step = (parent.Type == "GasGigant") ? (parent.Radius * 0.22f) : 1.2f;
        float baseDist = clearance + (moonOrder * step);

        m.Perihelion = baseDist * rng.RandfRange(0.96f, 0.99f);
        m.Aphelion = baseDist * rng.RandfRange(1.01f, 1.05f);
        m.Distance = (m.Aphelion + m.Perihelion) * 0.5f;

        m.Inclination = rng.RandfRange(0.0f, 360.0f);
        m.OrbitYaw = rng.RandfRange(0.0f, 360.0f);

        float halfR = m.Radius * 0.5f;
        m.Mass = (halfR * halfR) * rng.RandfRange(0.35f, 1.10f);
        SystemValues.Instance.CalculateGravity(ref m);

        m.Temperature = parent.Temperature + rng.RandfRange(-10.0f, 10.0f);
        m.Squish = 1.0f;
        m.AxialTilt = rng.RandfRange(-15.0f, 15.0f);


        float parentPull = Mathf.Sqrt(Mathf.Max(parent.Mass, 0.1f));
        m.BaseSpeed = (1.2f * parentPull / Mathf.Pow(Mathf.Max(m.Distance, 0.1f), 1.5f)) * rng.RandfRange(0.9f, 1.1f);

   
        m.rotationspeed = m.BaseSpeed;

        PlanetPackets.GenerateSkyColors(ref m);
        PlanetPackets.GenerateChemicals(ref m);

        return m;
    }
}