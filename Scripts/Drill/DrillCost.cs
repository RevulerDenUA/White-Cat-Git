using System.Collections.Generic;

// ===== ВАРТІСТЬ ВСТАНОВЛЕННЯ БУРА =====
// Впиши сюди, які ресурси й скільки списується зі сховища, коли ставиш бур.
// Назви мають точно збігатися з ResourceName у жилах (регістр не важливий).
// Порожній список — бур безкоштовний.
public static class DrillCost
{
	public static readonly Dictionary<string, int> Cost = new()
	{
		// { "Залізна руда", 20 },
		// { "Лід", 5 },
	};
}
