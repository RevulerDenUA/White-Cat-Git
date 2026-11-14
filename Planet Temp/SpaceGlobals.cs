using Godot;
using System.Collections.Generic;

public struct PlanetData
{
	public string type;
	public float Perihelion;  // (r_p) Найближча точка
	public float Aphelion;    // (r_a) Найдальша точка
	public float BaseSpeed;   // Базова кутова швидкість
	public float Inclination; // Нахил орбіти в градусах
	public float OrbitYaw;    // Поворот орбіти навколо зірки (0..360)

	public PlanetData(string Type, float perihelion, float aphelion, float baseSpeed, float inclination = 0f, float orbitYaw = 0f)
	{
		type = Type;
		Perihelion = Mathf.Min(perihelion, aphelion);
		Aphelion = Mathf.Max(perihelion, aphelion);
		BaseSpeed = baseSpeed;
		Inclination = inclination;
		OrbitYaw = orbitYaw;
	}
}

public static class SpaceGlobals
{
	public static double WarpSpeed = 1.0;

	// Глобальний список усіх заспавнених планет (доступний звідусіль)
	public static List<PlanetOrbit> Planets = new List<PlanetOrbit>();

	public static void SetWarp(double multiplier)
	{
		WarpSpeed = Mathf.Max(0.0, multiplier);
	}

	// Глобальна RNG-функція, яка повертає випадкову структуру для планети № index
	public static PlanetData GenerateRandomPlanet(int index)
	{
		string[] types = { "Rocky", "GasGiant", "Dwarf", "Water" };
		string chosenType = types[GD.RandRange(0, types.Length - 1)];

		// Кожна наступна планета (index = 0, 1, 2...) спавниться далі від зірки
		float minDistance = 8.0f + (index * 10.0f);
		float perihelion = minDistance + (float)GD.RandRange(-1.5, 2.0);
		float aphelion = perihelion + (float)GD.RandRange(2.0, 12.0);

		// За 3-м законом Кеплера: чим далі планета, тим менша базова швидкість
		float a = (aphelion + perihelion) * 0.5f;
		float speed = (8.0f / Mathf.Sqrt(a)) * (float)GD.RandRange(0.8, 1.2);

		// Випадковий нахил і розворот орбіти
		float inclination = (float)GD.RandRange(-18.0, 18.0);
		float yaw = (float)GD.RandRange(0.0, 360.0);

		return new PlanetData(chosenType, perihelion, aphelion, speed, inclination, yaw);
	}
}
