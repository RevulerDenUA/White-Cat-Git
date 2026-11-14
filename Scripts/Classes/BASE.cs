using System;
using Godot;

// Notice this doesn't even need to inherit from Node!
public class GameData
{
	// 'static' makes it globally accessible
	public static int CameraIndex = 0;
	public static bool InSpectatorMode = true;
	// Машина, якою зараз керує гравець (та, до якої прикріплена активна камера)
	public static Node3D ActiveVehicle = null;
}
// GameData.active
