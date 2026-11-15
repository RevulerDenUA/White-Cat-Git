using System.Collections.Generic;

// ===== ВАРТІСТЬ ВСТАНОВЛЕННЯ БУРА =====
// Що й скільки списується зі сховища, коли ставиш бур.
// Назви мають точно збігатися з ResourceName у жилах (регістр не важливий).
// Порожній список — бур безкоштовний.
public static class DrillCost
{
	public static readonly Dictionary<string, int> Cost = new()
	{
		 { "Титанова руда", 20 },
		 { "Мідна руда", 10},
		 { "Лід" , 1}
	};

	// Скільки повертається, коли бур розбираєш (1 = повністю, 0.5 = половина).
	// Погоджене повернення половини вартості; цілі предмети округлюються вниз.
	public const float RefundRatio = 0.5f;

	// Що гравець отримує на початку нової гри.
	// Стартовий капітал — рівно на 3 бури
	public const int StartDrills = 3;
	public static Dictionary<string, int> StartResources { get { var r = new Dictionary<string, int>(); foreach (var (k, v) in Cost) r[k] = v * StartDrills; return r; } }

	// Якщо в гравця не лишилось жодного бура і не вистачає ресурсів на новий —
	// він безкоштовно отримує те, чого бракує на 1 бур (щоб не застрягнути).
	public const bool SoftlockProtection = true;
}
