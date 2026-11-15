using System;

public class JeepData
{
	public static float MaxEngineForce = 100.0f;
	public static float MaxSteerAngle = 0.4f;
	public static float SteerSpeed = 2f;   // рад/с — кермо повертається плавно, а не миттєво
	public static float Speed = 15f;      // 200 * 15 = 3000 Н на кожне ведуче колесо (це сила, не потужність)
	public static float SteerSpeedFalloff = 0.08f; // що більше — то менше кермо на швидкості
	public static float AntiRollBarStiffness = 0f;  // стабілізатор крену. Тести: на нерівностях краще 0 (колеса ходять незалежно); 3–10 — менший крен у поворотах
	public static float AntiFlipAngle = 35f;       // градусів — з якого нахилу вмикається анти-перекидання
	public static float AntiFlipStrength = 8f;     // сила вирівнювання при великому нахилі
	public static float AntiFlipDamping = 2f;      // гасить розгойдування при великому нахилі
	public static float IdleBrake = 40f;           // сила ручника, коли газ не натиснутий
	public static float IdleBrakeSpeed = 1.5f;     // м/с — нижче цієї швидкості ручник вмикається
}
