using Godot;
using System;
using System.Collections.Generic;
using static Godot.GD;

public partial class Jeep : VehicleBody3D
{
    float steerInput;
    float gasInput;
    [Export] Label label;

    [Export] public Node3D ExitPoint;


    // Деталі коліс моделі Rover, прив'язані до фізичних коліс VehicleWheel3D
    private readonly List<(Node3D part, VehicleWheel3D wheel, Transform3D offset)> _wheelParts = new();

    public override void _Ready()
    {
        CacheWheelMounts();
        BindModelWheels();
    }

    public override void _Process(double delta)
    {
        // Колеса моделі повторюють фізичні: крутяться, повертають, ходять на підвісці
        foreach (var (part, wheel, offset) in _wheelParts)
            part.GlobalTransform = wheel.GlobalTransform * offset;
    }

    private void BindModelWheels()
    {
        var rover = GetNodeOrNull<Node3D>("Rover");
        if (rover == null) return;

        var wheels = new List<VehicleWheel3D>();
        foreach (var c in GetChildren())
            if (c is VehicleWheel3D w) wheels.Add(w);

        // Збираємо деталі коліс ("Left front | Tire", "Right rear | Alloy rim" і т.д.)
        var groups = new Dictionary<VehicleWheel3D, List<Node3D>>();
        foreach (var node in rover.FindChildren("*", "Node3D", true, false))
        {
            var n = (Node3D)node;
            string name = n.Name.ToString().ToLower();
            if (!(name.Contains("front |") || name.Contains("rear |"))) continue;

            // Найближче фізичне колесо (по горизонталі, в координатах машини)
            Vector3 lp = ToLocal(n.GlobalPosition);
            VehicleWheel3D best = null;
            float bestDist = float.MaxValue;
            foreach (var w in wheels)
            {
                float d = new Vector2(lp.X - w.Position.X, lp.Z - w.Position.Z).Length();
                if (d < bestDist) { bestDist = d; best = w; }
            }
            if (best == null) continue;
            if (!groups.ContainsKey(best)) groups[best] = new List<Node3D>();
            groups[best].Add(n);
        }

        foreach (var (wheel, parts) in groups)
        {
            // Центр колеса моделі = позиція шини (або перша деталь)
            Node3D tire = parts.Find(p => p.Name.ToString().ToLower().Contains("tire")) ?? parts[0];
            var center = new Transform3D(GlobalBasis.Orthonormalized(), tire.GlobalPosition);
            foreach (var part in parts)
                _wheelParts.Add((part, wheel, center.AffineInverse() * part.GlobalTransform));
        }

        if (_wheelParts.Count == 0)
            PushWarning("Jeep: не знайшов коліс у моделі Rover — вони не будуть крутитись");
    }

    public override void _PhysicsProcess(double delta)
    {
        ApplyStabilizers();

        if (!GameData.CanDrive(this))
        {
            EngineForce = 0;
            _throttle = 0;
            Brake = JeepData.IdleBrake; // ніхто не керує — машина стоїть на ручнику
            return;
        }
        steerInput = 0.0f;
        

        if (ControlInput.IsPressed("lefty"))
        {
            steerInput = 1.0f;
        }
        else if (ControlInput.IsPressed("righty"))
        {
            steerInput = -1.0f;
        }

        // На швидкості кермо повертає менше — інакше марсохід перекидається в поворотах
        float speed = LinearVelocity.Length();
        float steerLimit = JeepData.MaxSteerAngle / (1.0f + speed * JeepData.SteerSpeedFalloff);
        float targetSteer = steerInput * steerLimit;
        Steering = Mathf.MoveToward(Steering, targetSteer, JeepData.SteerSpeed * (float)delta);


        gasInput = 0.0f;
        if (ControlInput.IsPressed("forwardy")) gasInput = 1f;
        else if (ControlInput.IsPressed("backwardy")) gasInput = -1f;

        // «педаль» натискається плавно, відпускається швидко
        float rate = (Mathf.Abs(gasInput) > Mathf.Abs(_throttle) && Mathf.Sign(gasInput) == Mathf.Sign(_throttle) || _throttle == 0f)
            ? 1f / Mathf.Max(0.01f, JeepData.ThrottleRiseTime)
            : 4f;
        _throttle = Mathf.MoveToward(_throttle, gasInput, rate * (float)delta);

        EngineForce = _throttle == 0f ? 0f : _throttle * DriveForce(_throttle > 0) / Mathf.Max(1, _tractionWheels);

        // Без газу на малій швидкості — ручник, щоб машина не котилась і не гойдалась
        Brake = (gasInput == 0f && LinearVelocity.Length() < JeepData.IdleBrakeSpeed) ? JeepData.IdleBrake : 0f;

    }

    // ---------- Двигун ----------

    private int _tractionWheels;
    private float _throttle; // плавна «педаль газу», -1..1

    // Сумарна тяга на всі ведучі колеса, Н.
    // Тягне з повною силою на малій швидкості, далі — скільки дозволяє потужність,
    // і плавно вимикається біля максимальної швидкості.
    private float DriveForce(bool forward)
    {
        float v = LinearVelocity.Dot(GlobalBasis.Z);     // швидкість уперед (+Z), м/с
        if (!forward) v = -v;
        float maxV = forward ? JeepData.MaxSpeed : JeepData.MaxReverseSpeed;

        float force = Mathf.Min(JeepData.MaxTractionForce, JeepData.EnginePower / Mathf.Max(Mathf.Abs(v), 0.1f));
        // останні 20% швидкості — плавне зменшення до нуля
        float limit = Mathf.Clamp((maxV - v) / (maxV * 0.2f), 0f, 1f);
        return force * limit;
    }

    // ---------- Стабілізатори ----------

    // Точки кріплення коліс у координатах машини (до того, як фізика почне рухати колеса)
    private readonly Dictionary<VehicleWheel3D, Vector3> _wheelMounts = new();

    private void CacheWheelMounts()
    {
        foreach (var c in GetChildren())
            if (c is VehicleWheel3D w)
            {
                _wheelMounts[w] = w.Position;
                if (w.UseAsTraction) _tractionWheels++;
            }
    }

    // Наскільки стиснута пружина колеса, м (0 — колесо в повітрі / повністю розтиснуте)
    private float Compression(VehicleWheel3D w)
    {
        if (!w.IsInContact()) return 0f;
        Vector3 mount = ToGlobal(_wheelMounts[w]);
        float length = (mount - w.GlobalPosition).Dot(-GlobalBasis.Y); // поточна довжина пружини
        return Mathf.Max(0f, w.WheelRestLength - length);
    }

    private void ApplyStabilizers()
    {
        // 1) Стабілізатор поперечної стійкості — як у справжньої машини.
        //    Порівнює стиснення лівого й правого колеса однієї осі і підтискає менш стиснуте.
        //    На рівній дорозі й прямих схилах не робить нічого, діє лише в кренах.
        ApplyAntiRollBar(true);
        ApplyAntiRollBar(false);

        // 2) Анти-перекидання: вмикається тільки при великому нахилі
        Vector3 up = GlobalBasis.Y;
        float tilt = Mathf.RadToDeg(up.AngleTo(Vector3.Up));
        if (tilt > JeepData.AntiFlipAngle)
        {
            float k = Mathf.Clamp((tilt - JeepData.AntiFlipAngle) / 30f, 0f, 1f);
            Vector3 axis = up.Cross(Vector3.Up).Normalized();
            Vector3 tiltVel = AngularVelocity - Vector3.Up * AngularVelocity.Dot(Vector3.Up);
            ApplyTorque((axis * JeepData.AntiFlipStrength * k - tiltVel * JeepData.AntiFlipDamping * k) * Mass);
        }
    }

    private void ApplyAntiRollBar(bool front)
    {
        VehicleWheel3D left = null, right = null;
        foreach (var (w, mount) in _wheelMounts)
        {
            if ((mount.Z > 0) != front) continue;       // перед машини = +Z
            if (mount.X > 0) left = w; else right = w;
        }
        if (left == null || right == null) return;
        // колесо в повітрі — стабілізатор не працює (інакше він би підкидав машину)
        if (!left.IsInContact() || !right.IsInContact()) return;

        float diff = Compression(left) - Compression(right);
        if (Mathf.IsZeroApprox(diff)) return;

        // Пара сил: стиснуту сторону піднімаємо, протилежну притискаємо — рівно настільки ж.
        // Сумарна вертикальна сила = 0, тож машину не підкидає, лише гаситься крен.
        float force = diff * JeepData.AntiRollBarStiffness * Mass;
        Vector3 up = GlobalBasis.Y;
        ApplyForce(up * force, left.GlobalPosition - GlobalPosition);
        ApplyForce(-up * force, right.GlobalPosition - GlobalPosition);
    }
}
