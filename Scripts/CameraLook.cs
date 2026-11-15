using Godot;

// Повертає камеру вліво/вправо стрілками (дії CameraLeft / CameraRight),
// тільки коли ця камера активна. Кут обмежений MaxAngle.
public partial class CameraLook : Camera3D
{
	[Export] public float TurnSpeed = 30f;   // градусів за секунду
	[Export] public float MaxAngle = 45f;   // максимум вліво/вправо від початкового положення
	[Export] public bool ReturnToCenter = false; // повертатись назад, коли кнопку відпущено

	private Basis _startBasis;
	private float _yaw; // градуси

	public override void _Ready()
	{
		_startBasis = Basis;
	}

	public override void _Process(double delta)
	{
		if (!Current) return;

		float input = Input.GetAxis("CameraRight", "CameraLeft");
		if (input != 0f)
			_yaw += input * TurnSpeed * (float)delta;
		else if (ReturnToCenter)
			_yaw = Mathf.MoveToward(_yaw, 0f, TurnSpeed * (float)delta);

		_yaw = Mathf.Clamp(_yaw, -MaxAngle, MaxAngle);

		// Обертаємо навколо вертикальної осі батька, щоб нахил камери зберігався
		Basis = new Basis(Vector3.Up, Mathf.DegToRad(_yaw)) * _startBasis;
	}
}
