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

        if (GameData.InSpectatorMode || GameData.ActiveVehicle != this)
        {
            EngineForce = 0;
            return;
        }
        steerInput = 0.0f;
        

        if (Input.IsActionPressed("lefty"))
        {
            steerInput = 1.0f;
        }
        else if (Input.IsActionPressed("righty"))
        {
            steerInput = -1.0f;
        }

        // На швидкості кермо повертає менше — інакше марсохід перекидається в поворотах
        float speed = LinearVelocity.Length();
        float steerLimit = JeepData.MaxSteerAngle / (1.0f + speed * JeepData.SteerSpeedFalloff);
        float targetSteer = steerInput * steerLimit;
        Steering = Mathf.MoveToward(Steering, targetSteer, JeepData.SteerSpeed * (float)delta);


        gasInput = 0.0f;

        if (Input.IsActionPressed("forwardy"))
        {
            gasInput = JeepData.Speed;
        }
        else if (Input.IsActionPressed("backwardy"))
        {
            gasInput = -JeepData.Speed;
        }

        EngineForce = gasInput * JeepData.MaxEngineForce;

        ApplyAntiRoll();
    }

    // Стабілізатор: м'яко тягне корпус до вертикалі, коли він нахиляється вбік/вперед.
    // Діє тільки на крен і тангаж, рискання (поворот) не чіпає.
    private void ApplyAntiRoll()
    {
        Vector3 up = GlobalTransform.Basis.Y;
        Vector3 axis = up.Cross(Vector3.Up); // вісь, навколо якої треба повернути корпус
        Vector3 yawVel = Vector3.Up * AngularVelocity.Dot(Vector3.Up);
        Vector3 tiltVel = AngularVelocity - yawVel;

        Vector3 torque = axis * JeepData.AntiRollStrength - tiltVel * JeepData.AntiRollDamping;
        ApplyTorque(torque * Mass);
    }
}