using Godot;
using System.Collections.Generic;

// Керування машинами з затримкою сигналу (як дистанційне керування марсоходом).
// Замість Input.IsActionPressed("...") машини питають ControlInput.IsPressed("...") —
// і отримують стан кнопок, яким він був DelaySeconds тому.
public static class ControlInput
{
	public static float DelaySeconds = 0.15f;  // 0 — без затримки
	public static float JitterSeconds = 0.02f; // невелике «плавання» затримки, як у реального сигналу

	// дії, які проходять через затримку
	private static readonly string[] Actions = { "forwardy", "backwardy", "lefty", "righty", "spacy", "flight_descend" };
	private const int MaxHistorySamples = 512;

	private struct Sample { public double Time; public uint Mask; }
	private static readonly List<Sample> _history = new();
	private static ulong _lastFrame = ulong.MaxValue;
	private static double _jitter;
	private static readonly RandomNumberGenerator _rng = new();

	// Delayed commands belong to one piloting session, never to terminal text or another vehicle.
	public static void Reset()
	{
		_history.Clear();
		_lastFrame = ulong.MaxValue;
		_jitter = 0;
	}

	public static bool IsPressed(string action)
	{
		if (GameData.Context != ControlContext.Vehicle || !GameData.InputArmed) return false;
		int bit = System.Array.IndexOf(Actions, action);
		if (bit < 0) return Input.IsActionPressed(action); // невідома дія — без затримки
		if (DelaySeconds <= 0f) return (CurrentMask() & (1u << bit)) != 0;
		return (Delayed() & (1u << bit)) != 0;
	}

	private static uint CurrentMask()
	{
		uint m = 0;
		for (int i = 0; i < Actions.Length; i++)
			if (Input.IsActionPressed(Actions[i])) m |= 1u << i;
		return m;
	}

	private static double Now => Time.GetTicksUsec() / 1_000_000.0;

	// Записуємо стан раз на фізичний кадр і віддаємо той, що був DelaySeconds тому
	private static uint Delayed()
	{
		double now = Now;
		ulong frame = Engine.GetPhysicsFrames();
		if (frame != _lastFrame)
		{
			_lastFrame = frame;
			// довга перерва (ніхто не керував) — стара історія вже не актуальна
			if (_history.Count > 0 && now - _history[^1].Time > 0.25)
				_history.Clear();
			_history.Add(new Sample { Time = now, Mask = CurrentMask() });
			if (_history.Count > MaxHistorySamples) _history.RemoveAt(0);
			_jitter = Mathf.Lerp(_jitter, _rng.RandfRange(-JitterSeconds, JitterSeconds), 0.05);
		}

		double target = now - Mathf.Max(0f, DelaySeconds + (float)_jitter);
		// останній запис, не новіший за target
		uint result = 0;
		int keepFrom = 0;
		for (int i = 0; i < _history.Count; i++)
		{
			if (_history[i].Time > target) break;
			result = _history[i].Mask;
			keepFrom = i;
		}
		// старіше за потрібне — викидаємо (залишаючи один запис як поточний стан)
		if (keepFrom > 0) _history.RemoveRange(0, keepFrom);
		return result;
	}
}
