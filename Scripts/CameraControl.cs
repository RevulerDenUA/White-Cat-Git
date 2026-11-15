using Godot;
using Godot.Collections;

// Перемикає камери машин (C) і режим спостерігача (F).
// Також керує шейдером-фільтром: ч/б або кольоровий режим і сила перешкод,
// яка росте з висотою (Y) і швидкістю машини, до якої прикріплена камера.
public partial class CameraControl : Node
{
	[Export] public Array<Camera3D> Cameras = new();
	// Камери з цього списку показують кольорову картинку, решта — ч/б
	[Export] public Array<Camera3D> ColorCameras = new();
	[Export] public Camera3D SpecCamera;
	[Export] public ColorRect Filter;

	[ExportGroup("Noise")]
	[Export] public float NoiseStartHeight = 3f;   // нижче — висота не додає перешкод
	[Export] public float NoiseFullHeight = 40f;   // тут перешкоди від висоти максимальні
	[Export] public float NoiseStartSpeed = 8f;    // м/с
	[Export] public float NoiseFullSpeed = 30f;    // м/с
	[Export] public float MaxNoise = 0.95f;        // 1 — повністю зламаний сигнал

	private ShaderMaterial _filterMat;

	public override void _Ready()
	{
		_filterMat = Filter?.Material as ShaderMaterial;
		if (_filterMat == null)
			GD.PushWarning("CameraControl: не призначено Filter (або в нього немає ShaderMaterial) — фільтр і кольорові камери не працюватимуть");
		SpecCamera?.MakeCurrent();
	}

	// GameData.CameraIndex — індекс камери, яка зараз (або востаннє) активна.
	// З режиму спостерігача C повертає на ту саму камеру, а в машині — перемикає на наступну.
	private void NextCamera()
	{
		if (Cameras.Count == 0) return;

		if (!GameData.InSpectatorMode)
			GameData.CameraIndex++;
		GameData.CameraIndex = ((GameData.CameraIndex % Cameras.Count) + Cameras.Count) % Cameras.Count;

		Camera3D cam = Cameras[GameData.CameraIndex];
		if (cam == null) return;
		cam.MakeCurrent();
		GameData.ActiveVehicle = FindVehicle(cam);
	}

	private static Node3D FindVehicle(Node node)
	{
		for (Node n = node.GetParent(); n != null; n = n.GetParent())
			if (n is PhysicsBody3D body) return body;
		return null;
	}

	private static float SpeedOf(Node3D vehicle) => vehicle switch
	{
		RigidBody3D rb => rb.LinearVelocity.Length(),       // VehicleBody3D теж RigidBody3D
		CharacterBody3D cb => cb.Velocity.Length(),
		_ => 0f
	};

	public override void _Process(double delta)
	{
		if (Input.IsActionJustPressed("CameraChange"))
		{
			NextCamera();
			GameData.InSpectatorMode = false;
		}
		else if (Input.IsActionJustReleased("SpectatorMode"))
		{
			SpecCamera.MakeCurrent();
			GameData.InSpectatorMode = true;
		}

		UpdateFilter();
	}

	private void UpdateFilter()
	{
		if (_filterMat == null) return;

		Camera3D current = GetViewport().GetCamera3D();
		Node3D vehicle = GameData.InSpectatorMode ? null : GameData.ActiveVehicle;

		float noise = 0f;
		if (vehicle != null)
		{
			float h = Mathf.Clamp((vehicle.GlobalPosition.Y - NoiseStartHeight) / (NoiseFullHeight - NoiseStartHeight), 0f, 1f);
			float s = Mathf.Clamp((SpeedOf(vehicle) - NoiseStartSpeed) / (NoiseFullSpeed - NoiseStartSpeed), 0f, 1f);
			// обидва джерела складаються, але не перевищують 1
			noise = (1f - (1f - h) * (1f - s)) * MaxNoise;
		}

		_filterMat.SetShaderParameter("signal_quality", 1f - noise);
		_filterMat.SetShaderParameter("color_mode", current != null && ColorCameras.Contains(current));
	}
}
