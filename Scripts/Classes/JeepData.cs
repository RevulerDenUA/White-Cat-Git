using System;

public class JeepData
{
	// ---- Двигун (електричний, як у ровера) ----
	// Тяга = min(MaxTractionForce, EnginePower / швидкість): на малій швидкості — повна тяга
	// (заїжджає на круті схили), на великій — обмежена потужністю.
	// Схил, на який заїде: sin(кут) = MaxTractionForce / (маса * g). 6000 Н, 1000 кг, g=9.8 → ~37°.
	public static float EnginePower = 4000f;       // Вт (≈ 5.4 к.с.) — разом на всі ведучі колеса; від неї залежить швидкість на підйомі
	public static float MaxTractionForce = 6000f;  // Н — максимальна тяга разом на всі ведучі колеса
	public static float MaxSpeed = 1.5f;           // м/с (5.4 км/год) — вище двигун не тягне
	public static float MaxReverseSpeed = 1.2f;    // м/с назад
	public static float ThrottleRiseTime = 1.2f;   // с — за скільки «педаль» плавно доходить до упору (м'який старт)
	public static float MaxSteerAngle = 0.4f;
	public static float SteerSpeed = 2f;   // рад/с — кермо повертається плавно, а не миттєво
	public static float SteerSpeedFalloff = 0.08f; // що більше — то менше кермо на швидкості
	public static float AntiRollBarStiffness = 0f;  // стабілізатор крену. Тести: на нерівностях краще 0 (колеса ходять незалежно); 3–10 — менший крен у поворотах
	public static float AntiFlipAngle = 35f;       // градусів — з якого нахилу вмикається анти-перекидання
	public static float AntiFlipStrength = 8f;     // сила вирівнювання при великому нахилі
	public static float AntiFlipDamping = 2f;      // гасить розгойдування при великому нахилі
	public static float IdleBrake = 40f;           // сила ручника, коли газ не натиснутий
	public static float IdleBrakeSpeed = 1.5f;     // м/с — нижче цієї швидкості ручник вмикається
}
