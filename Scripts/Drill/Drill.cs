using Godot;

// Бур: розкладає опори (підлаштовуються під рельєф), свердлить і накопичує ресурс.
// Забирати накопичене — через DrillManager (марсохід/дрон під'їжджає й натискає G).
public partial class Drill : StaticBody3D
{
	public enum State { Deploying, Drilling, Full }

	[Export] public string ResourceName = "Залізна руда";
	[Export] public float ItemsPerMinute = 6f;
	[Export] public int Capacity = 50;
	[Export] public float DeployTime = 2.5f;     // с — опускання опор і каретки
	[Export] public float AugerSpeed = 14f;      // рад/с
	[Export] public float FeedCycle = 6f;        // с — один повний цикл буріння
	[Export] public float AugerExtend = 0.3f;    // м — наскільки шнек висувається нижче каретки (глибше в ґрунт)
	[Export(PropertyHint.Layers3DPhysics)] public uint GroundMask = 2;

	public State CurrentState { get; private set; } = State.Deploying;
	public Resources Stored { get; } = new Resources();
	public int StoredAmount => Stored.GetAmount(ResourceName);
	public ResourceVein Vein { get; set; }

	private float _progress;   // дробова частина видобутку
	private float _timer;

	private Node3D _carriage, _auger, _model;
	private readonly Node3D[] _legs = new Node3D[4];
	private readonly Node3D[] _feet = new Node3D[4];
	private readonly Vector3[] _legBase = new Vector3[4];
	private readonly Basis[] _footBase = new Basis[4];
	private readonly float[] _legTarget = new float[4];   // висування кожної ніжки після підлаштування
	private readonly Basis[] _footTarget = new Basis[4];
	private Vector3 _carriageBase, _augerBase, _modelBase;

	// Межі руху з README моделі
	private const float CarriageUp = 0.10f, CarriageDown = -0.22f, LegTravel = 0.10f, FootMaxTilt = 20f;

	public override void _Ready()
	{
		_model = GetNodeOrNull<Node3D>("Model");
		_carriage = FindChild("Carriage", true, false) as Node3D;
		_auger = FindChild("Auger_Rotating", true, false) as Node3D;
		string[] ids = { "FL", "FR", "RL", "RR" };
		for (int i = 0; i < 4; i++)
		{
			_legs[i] = FindChild("Leg_" + ids[i], true, false) as Node3D;
			_feet[i] = FindChild("Foot_" + ids[i], true, false) as Node3D;
			if (_legs[i] != null) _legBase[i] = _legs[i].Position;
			if (_feet[i] != null) _footBase[i] = _feet[i].Basis;
			_legTarget[i] = 0f;
			_footTarget[i] = _footBase[i];
		}
		if (_carriage != null) _carriageBase = _carriage.Position;
		if (_auger != null) _augerBase = _auger.Position;
		if (_model != null) _modelBase = _model.Position;

		if (StoredAmount >= Capacity) CurrentState = State.Full;
		ApplyPose(0f, CarriageUp, 0f, 0f);
	}

	// ---------- Адаптивні ніжки ----------

	// Викликати після того, як бур поставили на місце (GlobalTransform вже виставлений).
	// Кожна ніжка кидає промінь вниз; корпус піднімається/опускається так, щоб усі ніжки
	// вклались у свій хід ±0.1 м, а ступні нахиляються під поверхню (до 20°).
	public void AdaptLegs()
	{
		var space = GetWorld3D().DirectSpaceState;
		var hitY = new float[4];
		var hitN = new Vector3[4];
		var hasHit = new bool[4];
		float min = float.MaxValue, max = float.MinValue;

		for (int i = 0; i < 4; i++)
		{
			if (_legs[i] == null) continue;
			// точка під ніжкою на рівні «землі» моделі (y = 0 у координатах бура)
			Vector3 local = new(_legBase[i].X, 0f, _legBase[i].Z);
			Vector3 from = ToGlobal(local + Vector3.Up * 0.5f);
			Vector3 to = ToGlobal(local + Vector3.Down * 0.6f);
			var q = PhysicsRayQueryParameters3D.Create(from, to, GroundMask);
			q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
			var hit = space.IntersectRay(q);
			if (hit.Count == 0) continue;

			hasHit[i] = true;
			hitY[i] = ToLocal((Vector3)hit["position"]).Y;
			hitN[i] = (GlobalBasis.Inverse() * (Vector3)hit["normal"]).Normalized();
			min = Mathf.Min(min, hitY[i]);
			max = Mathf.Max(max, hitY[i]);
		}
		if (min == float.MaxValue) return; // жодна ніжка не знайшла землю

		// зсуваємо корпус у середину між найвищою й найнижчою точкою
		float shift = (min + max) * 0.5f;
		if (Mathf.Abs(shift) > 0.001f)
			GlobalPosition += GlobalBasis.Y * shift;

		for (int i = 0; i < 4; i++)
		{
			if (!hasHit[i]) { _legTarget[i] = -LegTravel; continue; } // висить — витягуємо до упору
			_legTarget[i] = Mathf.Clamp(hitY[i] - shift, -LegTravel, LegTravel);

			// нахил ступні під нормаль поверхні, обмежений шарніром
			Vector3 n = hitN[i];
			float angle = Mathf.Min(Vector3.Up.AngleTo(n), Mathf.DegToRad(FootMaxTilt));
			Vector3 axis = Vector3.Up.Cross(n);
			_footTarget[i] = axis.LengthSquared() < 1e-6f
				? _footBase[i]
				: new Basis(axis.Normalized(), angle) * _footBase[i];
		}
	}

	// ---------- Видобуток ----------

	// Миттєво поставити в робочий стан (для бурів, завантажених зі збереження)
	public void SkipDeploy()
	{
		CurrentState = StoredAmount >= Capacity ? State.Full : State.Drilling;
		_timer = 0f;
		ApplyPose(0f, 0f, 1f, 0f);
	}

	// Додати видобуток за час, коли гри не було (секунди). Повертає, скільки додано.
	public int AddOfflineTime(double seconds)
	{
		if (seconds <= 0) return 0;
		int before = StoredAmount;
		Produce((float)(seconds / 60.0 * ItemsPerMinute));
		return StoredAmount - before;
	}

	public void SetProgress(float p) => _progress = Mathf.Clamp(p, 0f, 0.999f);
	public float Progress => _progress;

	public void TakeAllTo(Resources target)
	{
		foreach (var (name, amount) in Stored.GetAll())
		{
			target.Add(name, amount);
			Stored.Remove(name, amount);
		}
		if (CurrentState == State.Full) { CurrentState = State.Drilling; _timer = 0f; }
	}

	private void Produce(float items)
	{
		int free = Capacity - StoredAmount;
		if (free <= 0) { CurrentState = State.Full; _progress = 0f; return; }

		_progress += items;
		int whole = Mathf.Min((int)_progress, free);
		if (whole > 0)
		{
			Stored.Add(ResourceName, whole);
			_progress -= whole;
		}
		if (StoredAmount >= Capacity) { CurrentState = State.Full; _progress = 0f; }
	}

	// ---------- Анімація ----------

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_timer += dt;

		switch (CurrentState)
		{
			case State.Deploying:
			{
				float t = Mathf.Clamp(_timer / DeployTime, 0f, 1f);
				float e = t * t * (3f - 2f * t); // плавний старт/зупинка
				ApplyPose(0f, Mathf.Lerp(CarriageUp, 0f, e), e, 0f);
				if (t >= 1f) { CurrentState = State.Drilling; _timer = 0f; }
				break;
			}
			case State.Drilling:
			{
				Produce(ItemsPerMinute / 60f * dt);

				// 0–55%: каретка повільно йде вниз до упору
				// 55–85%: шнек висувається з каретки ще нижче, в ґрунт
				// 85–100%: усе швидко піднімається
				float c = (_timer % FeedCycle) / FeedCycle;
				float feed, extend;
				if (c < 0.55f) { feed = Mathf.Lerp(0f, CarriageDown, c / 0.55f); extend = 0f; }
				else if (c < 0.85f) { feed = CarriageDown; extend = Mathf.Lerp(0f, AugerExtend, (c - 0.55f) / 0.3f); }
				else { float r = (c - 0.85f) / 0.15f; feed = Mathf.Lerp(CarriageDown, 0f, r); extend = Mathf.Lerp(AugerExtend, 0f, r); }

				_auger?.RotateObjectLocal(Vector3.Up, AugerSpeed * dt);
				ApplyPose(feed, 0f, 1f, extend);

				if (_model != null) // легка вібрація корпусу
					_model.Position = _modelBase + new Vector3(
						Mathf.Sin(_timer * 53f) * 0.004f, 0f, Mathf.Cos(_timer * 47f) * 0.004f);
				break;
			}
			case State.Full:
				// шнек зупинений і втягнутий, каретка піднята
				if (_carriage != null)
					_carriage.Position = _carriage.Position.Lerp(_carriageBase + Vector3.Up * CarriageUp, dt * 2f);
				if (_auger != null)
					_auger.Position = _auger.Position.Lerp(_augerBase, dt * 2f);
				if (_model != null) _model.Position = _modelBase;
				break;
		}
	}

	// legBlend: 0 — ніжки складені (підняті), 1 — стоять на землі (адаптовані)
	private void ApplyPose(float carriageFeed, float carriageOffset, float legBlend, float augerExtend)
	{
		if (_carriage != null)
			_carriage.Position = _carriageBase + Vector3.Up * (carriageFeed + carriageOffset);
		if (_auger != null)
			_auger.Position = new Vector3(_augerBase.X, _augerBase.Y - augerExtend, _augerBase.Z);
		for (int i = 0; i < 4; i++)
		{
			if (_legs[i] != null)
				_legs[i].Position = _legBase[i] + Vector3.Up * Mathf.Lerp(LegTravel, _legTarget[i], legBlend);
			if (_feet[i] != null)
				_feet[i].Basis = _footBase[i].Slerp(_footTarget[i], legBlend);
		}
	}
}
