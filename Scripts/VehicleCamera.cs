using Godot;

// Камера машини або дрона:
//  • нахил від руху: при розгоні/гальмуванні нахиляється вперед-назад,
//    у поворотах — нахиляється вбік (як справжній дрон або голова водія);
//  • (опційно) огляд вліво/вправо стрілками (CameraLeft / CameraRight).
// Кути задаються в градусах. Знак «+» у Pitch — ніс униз, у Roll — нахил у бік повороту.
public partial class VehicleCamera : Camera3D
{
	[ExportGroup("Огляд стрілками")]
	[Export] public bool EnableLook = false;
	[Export] public float TurnSpeed = 30f;        // градусів за секунду
	[Export] public float MaxAngle = 45f;         // максимум вліво/вправо від початкового положення
	[Export] public bool ReturnToCenter = false;  // повертатись назад, коли кнопку відпущено

	[ExportGroup("Нахил від руху")]
	[Export] public bool EnableTilt = true;
	[Export] public float PitchPerSpeed = 0.45f;  // °/(м/с) — постійний нахил униз при русі вперед (дрон)
	[Export] public float PitchPerAccel = 0.6f;   // °/(м/с²) — розгін: + ніс униз, − ніс угору
	[Export] public float RollPerYawRate = 6f;    // °/(рад/с) — нахил при повороті на місці
	[Export] public float RollPerLatAccel = 0.35f;// °/(м/с²) — нахил у повороті на швидкості
	[Export] public float MaxPitch = 14f;
	[Export] public float MaxRoll = 16f;
	[Export] public float Smoothing = 4f;         // більше — швидше реагує, менше — плавніше

	private Basis _startBasis;
	private float _yaw;               // огляд стрілками, градуси
	private float _pitch, _roll;      // поточний нахил, градуси
	private float _targetPitch, _targetRoll;

	private Node3D _vehicle;
	private Vector3 _prevFwd;
	private float _prevSpeed, _accel;
	private bool _hasPrev;
	private Node _settings;
	private Callable _settingsChanged;
	private bool _reducedMotion;

	public override void _Ready()
	{
		_startBasis = Basis;
		_settings = GetNodeOrNull<Node>("/root/UserSettings");
		_settingsChanged = Callable.From(ReadSettings);
		_settings?.Connect("changed", _settingsChanged);
		ReadSettings();
		for (Node n = GetParent(); n != null; n = n.GetParent())
			if (n is PhysicsBody3D body) { _vehicle = body; break; }
	}

	private Vector3 VehicleVelocity => _vehicle switch
	{
		RigidBody3D rb => rb.LinearVelocity,        // VehicleBody3D теж RigidBody3D
		CharacterBody3D cb => cb.Velocity,
		_ => Vector3.Zero
	};

	public override void _PhysicsProcess(double delta)
	{
		if (!EnableTilt || _reducedMotion || _vehicle == null) { _targetPitch = _targetRoll = 0; _hasPrev = false; return; }
		float dt = (float)delta;

		// перед машини/дрона = +Z, ліво = +X
		Basis vb = _vehicle.GlobalBasis.Orthonormalized();
		Vector3 local = vb.Inverse() * VehicleVelocity;
		float speed = local.Z;

		Vector3 fwd = vb.Z; fwd.Y = 0;
		fwd = fwd.LengthSquared() > 1e-6f ? fwd.Normalized() : Vector3.Forward;

		float yawRate = 0f;
		if (_hasPrev)
		{
			yawRate = _prevFwd.SignedAngleTo(fwd, Vector3.Up) / dt; // + поворот уліво
			float rawAccel = (speed - _prevSpeed) / dt;
			_accel = Mathf.Lerp(_accel, rawAccel, 1f - Mathf.Exp(-8f * dt)); // гасимо шум фізики
		}
		_prevFwd = fwd; _prevSpeed = speed; _hasPrev = true;

		float latAccel = yawRate * speed; // доцентрове прискорення в повороті
		_targetPitch = Mathf.Clamp(speed * PitchPerSpeed + _accel * PitchPerAccel, -MaxPitch, MaxPitch);
		_targetRoll = Mathf.Clamp(yawRate * RollPerYawRate + latAccel * RollPerLatAccel, -MaxRoll, MaxRoll);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		if (EnableLook && GameData.ActiveCamera == this && GameData.CanDrive(_vehicle) && !GetTree().Paused)
		{
			float input = Input.GetAxis("CameraRight", "CameraLeft");
			if (input != 0f) _yaw += input * TurnSpeed * dt;
			else if (ReturnToCenter) _yaw = Mathf.MoveToward(_yaw, 0f, TurnSpeed * dt);
			_yaw = Mathf.Clamp(_yaw, -MaxAngle, MaxAngle);
		}

		float k = 1f - Mathf.Exp(-Smoothing * dt);
		_pitch = _reducedMotion ? 0f : Mathf.Lerp(_pitch, _targetPitch, k);
		_roll = _reducedMotion ? 0f : Mathf.Lerp(_roll, _targetRoll, k);

		// нахил — у системі координат машини: тангаж навколо її осі X, крен навколо осі Z.
		// Нахил «у бік повороту вліво» = ліва сторона вниз = від'ємний кут навколо +Z.
		var tilt = new Basis(Vector3.Right, Mathf.DegToRad(_pitch)) * new Basis(Vector3.Back, Mathf.DegToRad(-_roll));
		Basis = tilt * new Basis(Vector3.Up, Mathf.DegToRad(_yaw)) * _startBasis;
	}

	private void ReadSettings() => _reducedMotion = _settings != null && _settings.Call("get_value", "reduced_motion").AsBool();

	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(_settings) && _settings.IsConnected("changed", _settingsChanged))
			_settings.Disconnect("changed", _settingsChanged);
	}
}
