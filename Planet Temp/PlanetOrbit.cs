using Godot;

public partial class PlanetOrbit : Node3D
{
	[Export] public WorldEnvironment WorldEnv;
	[Export] public Node3D StarNode;
	[Export] public Node3D OrbitRingMesh;
	[Export] public Node3D PlanetNode;
	[Export] public Marker3D PerihelionPoint;
	[Export] public Marker3D AphelionPoint;

	public PlanetData Data;
	public int OrbitIndex = 0;
	public bool _isClone = false;

	private float _a;
	private float _b;
	private float _c;
	private float _speed = 0.1f;
	private float _angleE = 0.0f;

	public double timerultra = 0.0f;

	public bool dimed = false;

	public void _on_local_planet_tree_entered ()
	{
	SystemValues.Instance.PlanetCount = SystemValues.Getplanet(SystemValues.Instance.SystemSeed);
	}

	public override void _Ready()
	{
		donotopen();
	}

	public void donotopen()
	{
		if (!_isClone)
			SystemValues.Planets.Clear();

		OrbitRingMesh = GetNodeOrNull<Node3D>("Orbit") ?? OrbitRingMesh;
		PlanetNode = GetNodeOrNull<Node3D>("Local Planet") ?? PlanetNode;
		PerihelionPoint = GetNodeOrNull<Marker3D>("Orbit/Marker3D") ?? PerihelionPoint;
		AphelionPoint = GetNodeOrNull<Marker3D>("Orbit/Marker3D2") ?? AphelionPoint;

		int seed = SystemValues.Instance.SystemSeed;
		int count = SystemValues.Instance.PlanetCount;

		string type = SpaceGlobals.manageroforder(seed, count, OrbitIndex);
		Data = SpaceGlobals.GeneratePlanet(seed, OrbitIndex, type);

		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Seed = (ulong)Mathf.Abs(Data.PlanetSeed);
		_angleE = rng.RandfRange(0.0f, Mathf.Tau);

		SetupOrbit(Data);
		SystemValues.Planets.Add(this);

		if (!_isClone && count > 1)
			SpawnClones(count - 1);
	}

	public void SpawnClones(int extraCount)
	{
		SystemValues.SpawnClones(this, extraCount);
	}

	public void SetupOrbit(PlanetData d)
	{
		_a = (d.Aphelion + d.Perihelion) * 0.5f;
		_b = Mathf.Sqrt(d.Aphelion * d.Perihelion);
		_c = (d.Aphelion - d.Perihelion) * 0.5f;
		_speed = d.BaseSpeed;

		float baseRadius = SystemValues.Instance.MeshBaseRadius;

		if (OrbitRingMesh != null)
		{
			OrbitRingMesh.Scale = new Vector3(_a / baseRadius, 1.0f, _b / baseRadius);
			OrbitRingMesh.Position = new Vector3(-_c, 0f, 0f);
		}

		if (PerihelionPoint != null)
			PerihelionPoint.Position = new Vector3(d.Perihelion, 0f, 0f);
		if (AphelionPoint != null)
			AphelionPoint.Position = new Vector3(-d.Aphelion, 0f, 0f);

		if (PlanetNode != null)
		{
			PlanetNode.Scale = Vector3.One * d.Radius;
			PlanetPackets.ApplyTexture(PlanetNode, d); 
		}
		RotationDegrees = new Vector3(d.Inclination, d.OrbitYaw, 0f);
	}

	public override void _Process(double delta)
	{

	if (!_isClone && WorldEnv?.Environment != null)
		{
			if (timerultra <= 0)
			{
				if (dimed)
				{
					WorldEnv.Environment.BackgroundEnergyMultiplier = 1.0f;
					dimed = false;
					timerultra = 0.1;
				}
				else
				{
					WorldEnv.Environment.BackgroundEnergyMultiplier = 0.8f;
					dimed = true; // <-- THIS WAS MISSING
					timerultra = 0.1;
				}
			}
			else
			{
				timerultra -= delta;
			}
		}

		if (PlanetNode == null) return;

		float dt = (float)(delta * SystemValues.WarpSpeed);

		float cosE = Mathf.Cos(_angleE);
		float sinE = Mathf.Sin(_angleE);

		float x = _a * cosE - _c;
		float z = _b * sinE;

		float dist = Mathf.Sqrt(x * x + z * z);
		if (dist < 0.001f) dist = _a;

		_angleE = (_angleE + _speed * (_a / dist) * dt) % Mathf.Tau;

		PlanetNode.Position = new Vector3(x, 0f, z);
		PlanetNode.RotateY(Data.rotationspeed * dt);
	}
}
