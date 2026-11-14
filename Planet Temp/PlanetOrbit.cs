using Godot;

public partial class PlanetOrbit : Node3D
{
	[Export] public int PlanetCount = 7;      // Впиши сюди кількість планет в Інспекторі!

	[Export] public Node3D StarNode;          // Зірка
	[Export] public Node3D OrbitRingMesh;     // Orbit
	[Export] public Node3D PlanetNode;        // Local Planet
	[Export] public Marker3D PerihelionPoint; // Marker3D
	[Export] public Marker3D AphelionPoint;   // Marker3D2

	[Export] public float MeshBaseRadius = 1.0f; // Залиш те значення, яке зараз працює у тебе (1.0 або 4.0)

	// Локальна структура конкретно цієї планети (можна читати через planet.Data)
	public PlanetData Data;
	public int OrbitIndex = 0;

	private bool _isClone = false;
	private float _a;
	private float _b;
	private float _c;
	private float _speed = 1.0f;
	private float _angleE = 0.0f;

	public override void _Ready()
	{
		// Гарантуємо, що кожен клон керує власними дочірніми нодами Orbit і Local Planet
		OrbitRingMesh = GetNodeOrNull<Node3D>("Orbit") ?? OrbitRingMesh;
		PlanetNode = GetNodeOrNull<Node3D>("Local Planet") ?? PlanetNode;
		PerihelionPoint = GetNodeOrNull<Marker3D>("Orbit/Marker3D") ?? PerihelionPoint;
		AphelionPoint = GetNodeOrNull<Marker3D>("Orbit/Marker3D2") ?? AphelionPoint;

		// Випадкова стартова точка на орбіті, щоб усі 7 планет не стартували в одній лінії
		_angleE = (float)GD.RandRange(0.0, Mathf.Tau);

		// Генеруємо випадкові параметри для цієї планети через глобальну функцію
		Data = SpaceGlobals.GenerateRandomPlanet(OrbitIndex);
		SetupOrbit(Data);

		SpaceGlobals.Planets.Add(this);

		// Якщо це перша (оригінальна) нода на сцені — спавнимо решту (PlanetCount - 1) планет
		if (!_isClone && PlanetCount > 1)
		{
			SpawnAdditionalPlanets(PlanetCount - 1);
		}
	}

	private void SpawnAdditionalPlanets(int extraCount)
	{
		Node parent = GetParent();
		for (int i = 1; i <= extraCount; i++)
		{
			PlanetOrbit clone = (PlanetOrbit)Duplicate();
			clone._isClone = true;
			clone.OrbitIndex = i;
			clone.StarNode = StarNode;
			clone.MeshBaseRadius = MeshBaseRadius;

			parent.CallDeferred(Node.MethodName.AddChild, clone);
		}
	}

	public void CalculateOrbit(float aphelion, float perihelion, float speed)
	{
		_a = (aphelion + perihelion) * 0.5f;
		_b = Mathf.Sqrt(aphelion * perihelion);
		_c = (aphelion - perihelion) * 0.5f;
		_speed = speed;
	}

	public void SetupOrbit(PlanetData data)
	{
		CalculateOrbit(data.Aphelion, data.Perihelion, data.BaseSpeed);

		float scaleX = _a / MeshBaseRadius;
		float scaleZ = _b / MeshBaseRadius;

		if (OrbitRingMesh != null)
		{
			OrbitRingMesh.Scale = new Vector3(scaleX, 1.0f, scaleZ);
			OrbitRingMesh.Position = new Vector3(-_c, 0f, 0f);
		}

		if (PerihelionPoint != null)
			PerihelionPoint.Position = new Vector3(data.Perihelion, 0f, 0f);
		if (AphelionPoint != null)
			AphelionPoint.Position = new Vector3(-data.Aphelion, 0f, 0f);

		RotationDegrees = new Vector3(data.Inclination, data.OrbitYaw, 0f);
	}

	public override void _Process(double delta)
	{
		if (StarNode == null || PlanetNode == null) return;

		float distance = StarNode.GlobalPosition.DistanceTo(PlanetNode.GlobalPosition);
		if (distance < 0.001f) distance = _a;

		float dE = (float)(_speed * (_a / distance) * delta * SpaceGlobals.WarpSpeed);
		_angleE = (_angleE + dE) % Mathf.Tau;

		float x = _a * Mathf.Cos(_angleE) - _c;
		float z = _b * Mathf.Sin(_angleE);

		PlanetNode.Position = new Vector3(x, 0f, z);
	}
}
