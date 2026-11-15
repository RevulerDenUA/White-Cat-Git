using System;

public class JeepData
{
	public static float MaxEngineForce = 200.0f;
	public static float MaxSteerAngle = 0.4f;
	public static float SteerSpeed = 2f;   // рад/с — кермо повертається плавно, а не миттєво
	public static float Speed = 15f;      // 200 * 15 = 3000 сили двигуна
	public static float SteerSpeedFalloff = 0.08f; // що більше — то менше кермо на швидкості
	public static float AntiRollStrength = 6f;     // сила вирівнювання корпусу
	public static float AntiRollDamping = 2f;      // гасить розгойдування
}
