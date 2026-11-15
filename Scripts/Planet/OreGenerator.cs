using Godot;
using System.Collections.Generic;

// Генератор рудних жил на поверхні планети (Scene/Vein.tscn → ResourceVein).
// Детермінований: та сама планета (PlanetSeed) — ті самі жили на тих самих місцях,
// тож шляхи до жил у збереженнях бурів (DrillManager) лишаються дійсними.
//
// Які руди — зі складу надр планети (PlanetPackets.GenerateChemicals, частки у %):
//   Титанова руда  ← залізо ×0.6 + силікати ×0.4   (багато на Iron-Rich і Silicate-Core)
//   Мідна руда     ← залізо ×0.5 + сірка ×3 + вуглець   (найбільше на Iron-Rich із сіркою)
//   Лід            ← водяний лід ×1.5 (+ невеликий фон на не гарячих світах);
//                    на гарячих (Iron-Rich, температура > 70) льоду немає
// Багатший склад → більше жил; більша частка руди → швидший видобуток і більший бур.
// Газові гіганти ("GasGigant") — жил немає. Без активної планети — змішаний набір для тестів.
public partial class OreGenerator : Node3D
{
	public const string Titanium = "Титанова руда";
	public const string Copper = "Мідна руда";
	public const string Ice = "Лід";

	[Export] public TerrainWorld Terrain;
	[Export] public PackedScene VeinScene;
	[Export] public float Radius = 160f;          // м від центру (шатл у початку координат)
	[Export] public float KeepOutRadius = 18f;    // майданчик шатла — без жил
	[Export] public float MinSpacing = 14f;       // мінімум між жилами
	[Export] public int MinVeins = 8;
	[Export] public int MaxVeins = 18;
	[Export] public float MaxSlope = 14f;         // градусів — на крутіших схилах камінь висить
	[Export] public int FallbackSeed = 12345;     // коли планету не вибрано

	private readonly List<ResourceVein> _veins = new();
	public IReadOnlyList<ResourceVein> Veins => _veins;

	// Вага кожної руди для поточної планети (для діагностики й UI)
	public IReadOnlyDictionary<string, float> Weights => _weights;
	private readonly Dictionary<string, float> _weights = new();

	private static readonly Dictionary<string, Color> MarkerColors = new()
	{
		[Titanium] = new Color(0.72f, 0.74f, 0.8f),
		[Copper] = new Color(0.85f, 0.45f, 0.25f),
		[Ice] = new Color(0.5f, 0.85f, 1f),
	};

	public override void _Ready()
	{
		VeinScene ??= GD.Load<PackedScene>("res://Scene/Vein.tscn");
		Terrain ??= FindTerrain();
		// Синхронно: DrillManager відкладено (CallDeferred) підв'язує бури до жил за шляхами,
		// тож жили мають існувати до того. Висоту терен рахує й до свого _Ready.
		Generate();
	}

	private TerrainWorld FindTerrain()
	{
		for (Node n = GetParent(); n != null; n = n.GetParent())
			if (n is TerrainWorld t) return t;
		return null;
	}

	public void Generate()
	{
		foreach (var v in _veins)
			if (IsInstanceValid(v)) v.QueueFree();
		_veins.Clear();
		_weights.Clear();

		if (Terrain == null || VeinScene == null)
		{
			GD.PushWarning("OreGenerator: не задано Terrain або VeinScene — жил не буде");
			return;
		}

		bool hasPlanet = CurrentGen.HasActivePlanet;
		PlanetData p = CurrentGen.ActivePlanet;

		if (hasPlanet && IsGas(p.Type)) return; // на газових гігантах нема твердої поверхні

		ComputeWeights(hasPlanet, p);
		float total = 0f;
		foreach (var w in _weights.Values) total += w;
		if (total <= 0.001f) return;

		var rng = new RandomNumberGenerator
		{
			Seed = (ulong)Mathf.Abs((long)(hasPlanet ? p.PlanetSeed : FallbackSeed) * 7919L + 4242L)
		};

		// Багатство надр: ~90 — звичайна планета (сума ваг кладеться приблизно 60…120)
		float richness = Mathf.Clamp(total / 90f, 0.6f, 1.4f);
		int count = Mathf.Clamp(Mathf.RoundToInt(rng.RandiRange(MinVeins, MaxVeins) * richness), MinVeins / 2, MaxVeins + 4);

		var placed = new List<Vector2>();
		int attempts = count * 40;
		while (placed.Count < count && attempts-- > 0)
		{
			float ang = rng.Randf() * Mathf.Tau;
			// sqrt — рівномірно по площі кільця
			float r = Mathf.Sqrt(rng.RandfRange(KeepOutRadius * KeepOutRadius, Radius * Radius));
			var xz = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;

			bool tooClose = false;
			foreach (var q in placed)
				if (q.DistanceTo(xz) < MinSpacing) { tooClose = true; break; }
			if (tooClose) continue;

			Vector3 world = GlobalTransform * new Vector3(xz.X, 0f, xz.Y);
			if (Slope(world.X, world.Z) > MaxSlope) continue;

			string resource = PickResource(rng, total);
			placed.Add(xz);
			SpawnVein(placed.Count - 1, resource, world, rng, _weights[resource] / total);
		}
	}

	private static bool IsGas(string type)
	{
		string t = type?.Trim().ToLowerInvariant() ?? "";
		return t is "gas" or "gasgigant" or "gasgiant" or "gas_giant";
	}

	private void ComputeWeights(bool hasPlanet, PlanetData p)
	{
		if (!hasPlanet)
		{
			_weights[Titanium] = 35f;
			_weights[Copper] = 30f;
			_weights[Ice] = 20f;
			return;
		}

		bool hot = p.CoreTemplate == "Iron-Rich" || p.Temperature > 70f;
		_weights[Titanium] = p.Iron * 0.6f + p.Silicates * 0.4f;
		_weights[Copper] = p.Iron * 0.5f + p.Sulfur * 3f + p.Carbon;
		_weights[Ice] = hot ? 0f : p.WaterIce * 1.5f + 4f;
	}

	private string PickResource(RandomNumberGenerator rng, float total)
	{
		float roll = rng.Randf() * total;
		foreach (var (name, w) in _weights)
		{
			if (roll < w) return name;
			roll -= w;
		}
		return Titanium;
	}

	// Нахил поверхні в градусах (центральні різниці на ±1.5 м)
	private float Slope(float x, float z)
	{
		const float d = 1.5f;
		float dx = (Terrain.GetHeightAt(x + d, z) - Terrain.GetHeightAt(x - d, z)) / (2f * d);
		float dz = (Terrain.GetHeightAt(x, z + d) - Terrain.GetHeightAt(x, z - d)) / (2f * d);
		return Mathf.RadToDeg(Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)));
	}

	private void SpawnVein(int index, string resource, Vector3 world, RandomNumberGenerator rng, float share)
	{
		var vein = VeinScene.Instantiate<ResourceVein>();
		// стабільне ім'я — DrillManager зберігає шлях до жили
		vein.Name = $"Vein_{index:00}";
		vein.ResourceName = resource;
		vein.MarkerColor = MarkerColors[resource];
		vein.ItemsPerMinute = Mathf.Snapped(4f + 8f * share, 0.5f);
		vein.DrillCapacity = 40 + Mathf.RoundToInt(40f * share);
		vein.RockSeed = (int)(rng.Randi() & 0x7FFFFFFF) | 1;

		// камінь стоїть на найнижчій точці під собою, щоб не висіти на схилі
		float y = Terrain.GetHeightAt(world.X, world.Z);
		const float foot = 1.6f;
		y = Mathf.Min(y, Terrain.GetHeightAt(world.X + foot, world.Z));
		y = Mathf.Min(y, Terrain.GetHeightAt(world.X - foot, world.Z));
		y = Mathf.Min(y, Terrain.GetHeightAt(world.X, world.Z + foot));
		y = Mathf.Min(y, Terrain.GetHeightAt(world.X, world.Z - foot));

		AddChild(vein);
		vein.GlobalPosition = new Vector3(world.X, y, world.Z);
		_veins.Add(vein);
	}
}
