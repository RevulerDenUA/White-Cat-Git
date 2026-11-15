using System.Collections.Generic;
using Godot;

// Стан шатла між сценами: де він сів і які апарати зараз зовні.
// Правило відльоту (погоджене з командою): шатл не летить на іншу планету,
// поки і марсохід, і Інжінюїті не всередині.
// Зберігається тільки на час сесії гри (між сценами), у файл не пишеться.
public static class ShuttleState
{
	public const string RoverId = "rover";
	public const string IngenuityId = "ingenuity";
	public const string PlanetScene = "res://Scene/WorldGen.tscn";

	public static readonly string[] VehicleIds = { RoverId, IngenuityId };

	// Камери апаратів у сцені планети (імена вузлів у нижньому регістрі, як у RemoteVehicle.CameraId)
	public static readonly Dictionary<string, string[]> VehicleCameras = new()
	{
		[RoverId] = new[] { "carcamera", "carcamera2" },
		[IngenuityId] = new[] { "camcolor", "cambw" },
	};

	public static bool HasPlanet { get; private set; }
	public static PlanetData Planet { get; private set; }

	// Апарат, яким гравець керуватиме після завантаження сцени планети
	public static string PendingVehicle { get; private set; } = "";
	public static string PendingCamera { get; private set; } = ""; // порожньо — перша камера апарата

	// Куди повертатись з планети (сцена з командною кімнатою, звідки запустили)
	public static string ReturnScene { get; private set; } = "res://Scene/A.tscn";

	// Апарати зовні шатла й де їх залишили
	private static readonly Dictionary<string, Transform3D> _outside = new();

	public static bool IsOutside(string id) => _outside.ContainsKey(id);
	public static bool TryGetOutside(string id, out Transform3D xf) => _outside.TryGetValue(id, out xf);
	public static IEnumerable<string> OutsideIds => _outside.Keys;
	public static int OutsideCount => _outside.Count;

	public static void SetOutside(string id, Transform3D xf) => _outside[id] = xf;
	public static void SetInside(string id) => _outside.Remove(id);

	public static string VehicleName(string id) => id switch
	{
		RoverId => "Марсохід",
		IngenuityId => "Інжінюїті",
		_ => id,
	};

	public static bool IsGas(PlanetData d) => d.Type == "GasGigant";

	public static bool IsCurrent(PlanetData d) => HasPlanet && Planet.PlanetSeed == d.PlanetSeed && Planet.IsMoon == d.IsMoon;

	// Чи може апарат летіти на цю планету. Порожній рядок — можна, інакше причина для гравця.
	public static string CanLaunch(PlanetData target, string vehicleId)
	{
		if (vehicleId == RoverId && IsGas(target))
			return "На газовій планеті немає твердої поверхні — сюди летить тільки Інжінюїті.";

		if (!IsCurrent(target) && OutsideCount > 0)
		{
			var names = new List<string>();
			foreach (string id in _outside.Keys) names.Add(VehicleName(id));
			return $"Шатл не злетить: {string.Join(" і ", names)} ще не в шатлі. Заведіть апарат у зону біля рампи.";
		}
		return "";
	}

	// Летимо (або лишаємось на тій самій планеті) і передаємо керування апарату
	public static string Launch(SceneTree tree, PlanetData target, string vehicleId, string returnScene, string cameraId = "")
	{
		string error = CanLaunch(target, vehicleId);
		if (error != "") return error;

		if (!IsCurrent(target))
		{
			Planet = target;
			HasPlanet = true;
			_outside.Clear();
		}

		if (!string.IsNullOrEmpty(returnScene)) ReturnScene = returnScene;
		PendingVehicle = vehicleId;
		PendingCamera = cameraId ?? "";
		CurrentGen.SavePlanetForTransition(target);
		tree.ChangeSceneToFile(PlanetScene);
		return "";
	}

	// Команда термінала «connect апарат[/камера]»: апарат висаджується на планету, де стоїть шатл.
	// Тестовий полігон кімнати гравцю недоступний. Порожній рядок — можна летіти.
	public static string CheckConnect(string address, out string vehicleId, out string cameraId)
	{
		vehicleId = cameraId = "";
		string[] parts = (address ?? "").Trim().ToLowerInvariant().Split('/');
		if (parts.Length > 2 || parts[0] == "")
			return "Формат: connect апарат або connect апарат/камера.";
		if (!VehicleCameras.TryGetValue(parts[0], out string[] cameras))
			return $"Апарат не знайдено. Доступні: {string.Join(", ", VehicleIds)}.";
		if (parts.Length == 2 && System.Array.IndexOf(cameras, parts[1]) < 0)
			return "Камеру не знайдено. Доступні адреси — у help.";
		if (!HasPlanet)
			return "Шатл ще на орбіті. Спершу виберіть планету в telescope.";
		string error = CanLaunch(Planet, parts[0]);
		if (error != "") return error;
		vehicleId = parts[0];
		cameraId = parts.Length == 2 ? parts[1] : "";
		return "";
	}

	// Тільки для тестів і налагодження
	public static void Reset()
	{
		HasPlanet = false;
		Planet = default;
		PendingVehicle = "";
		PendingCamera = "";
		_outside.Clear();
	}
}
