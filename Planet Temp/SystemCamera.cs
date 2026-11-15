using Godot;

// Камера огляду зоряної системи.
// ПКМ (затиснути) — обертати навколо точки огляду, СКМ — зсунути точку, колесо — наблизити/віддалити.
// ЛКМ не чіпає — вона для вибору планет (PlanetSelector). Курсор лишається видимим.
public partial class SystemCamera : Camera3D
{
	[Export] public float RotateSensitivity = 0.3f;   // градусів на піксель
	[Export] public float PanSensitivity = 0.0015f;   // частка відстані на піксель
	[Export] public float ZoomStep = 1.15f;           // у скільки разів змінюється відстань за один крок колеса
	[Export] public float MinDistance = 2f;
	[Export] public float MaxDistance = 2000f;
	[Export] public float MinPitch = -85f, MaxPitch = 85f;
	[Export] public float Smoothing = 12f;            // більше — різкіше

	private Vector3 _pivot, _pivotT;
	private float _yaw, _pitch, _dist;
	private float _yawT, _pitchT, _distT;
	private bool _rotating, _panning;
	private Node3D _follow; // вибрана планета — точка огляду їде за нею

	// Плавно перелетіти до тіла й тримати його в центрі (кут огляду зберігається)
	public void FocusOn(Node3D target, float distance)
	{
		_follow = target;
		_distT = Mathf.Clamp(distance, MinDistance, MaxDistance);
	}

	public void ClearFocus() => _follow = null;

	public override void _Ready()
	{
		// Точка, на яку камера дивиться зі сцени: перетин погляду з площиною орбіт (y = 0).
		// Так стартовий кадр лишається таким, як виставлено в редакторі.
		Vector3 fwd = -GlobalBasis.Z;
		_pivot = Vector3.Zero;
		if (Mathf.Abs(fwd.Y) > 0.01f)
		{
			float t = -GlobalPosition.Y / fwd.Y;
			if (t > 0f) _pivot = GlobalPosition + fwd * t;
		}

		Vector3 off = GlobalPosition - _pivot;
		_dist = Mathf.Clamp(off.Length(), MinDistance, MaxDistance);
		_yaw = Mathf.RadToDeg(Mathf.Atan2(off.X, off.Z));
		_pitch = Mathf.Clamp(Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(off.Y / Mathf.Max(off.Length(), 0.001f), -1f, 1f))), MinPitch, MaxPitch);

		_pivotT = _pivot; _yawT = _yaw; _pitchT = _pitch; _distT = _dist;
		Apply();
	}

	// Натискання — тільки якщо не по UI (панель планети тощо)
	public override void _UnhandledInput(InputEvent e)
	{
		if (e is not InputEventMouseButton { Pressed: true } mb) return;

		switch (mb.ButtonIndex)
		{
			case MouseButton.Right: _rotating = true; break;
			case MouseButton.Middle: _panning = true; break;
			case MouseButton.WheelUp: _distT = Mathf.Max(_distT / ZoomStep, MinDistance); break;
			case MouseButton.WheelDown: _distT = Mathf.Min(_distT * ZoomStep, MaxDistance); break;
			default: return;
		}
		GetViewport().SetInputAsHandled();
	}

	// Відпускання й рух — завжди, навіть якщо курсор заїхав на UI
	public override void _Input(InputEvent e)
	{
		if (e is InputEventMouseButton { Pressed: false } mb)
		{
			if (mb.ButtonIndex == MouseButton.Right) _rotating = false;
			if (mb.ButtonIndex == MouseButton.Middle) _panning = false;
		}
		else if (e is InputEventMouseMotion mm)
		{
			if (_rotating)
			{
				_yawT -= mm.ScreenRelative.X * RotateSensitivity;
				_pitchT = Mathf.Clamp(_pitchT + mm.ScreenRelative.Y * RotateSensitivity, MinPitch, MaxPitch);
			}
			if (_panning)
			{
				_follow = null; // ручний зсув скасовує стеження
				float k = _distT * PanSensitivity;
				_pivotT += (-GlobalBasis.X * mm.ScreenRelative.X + GlobalBasis.Y * mm.ScreenRelative.Y) * k;
			}
		}
	}

	public override void _Process(double delta)
	{
		// вікно втратило фокус і кнопку відпустили поза ним
		if (_rotating && !Input.IsMouseButtonPressed(MouseButton.Right)) _rotating = false;
		if (_panning && !Input.IsMouseButtonPressed(MouseButton.Middle)) _panning = false;

		if (_follow != null)
		{
			if (IsInstanceValid(_follow) && _follow.IsInsideTree()) _pivotT = _follow.GlobalPosition;
			else _follow = null;
		}

		float w = 1f - Mathf.Exp(-Smoothing * (float)delta);
		_yaw = Mathf.Lerp(_yaw, _yawT, w);
		_pitch = Mathf.Lerp(_pitch, _pitchT, w);
		_dist = Mathf.Lerp(_dist, _distT, w);
		_pivot = _pivot.Lerp(_pivotT, w);
		Apply();
	}

	private void Apply()
	{
		float y = Mathf.DegToRad(_yaw), p = Mathf.DegToRad(_pitch);
		var dir = new Vector3(Mathf.Sin(y) * Mathf.Cos(p), Mathf.Sin(p), Mathf.Cos(y) * Mathf.Cos(p));
		GlobalPosition = _pivot + dir * _dist;
		LookAt(_pivot, Vector3.Up);
	}
}
