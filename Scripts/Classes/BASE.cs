using System;

// Notice this doesn't even need to inherit from Node!
public class GameData
{
	// 'static' makes it globally accessible
	public static bool isPlayerInside = false;
	public static bool isPlayerNearDoor = false;
	public static int runtime = 0; //placeholder
}
// GameData.active
