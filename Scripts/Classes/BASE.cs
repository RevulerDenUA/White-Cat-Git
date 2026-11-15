using Godot;

public enum ControlContext { Room, Terminal, Vehicle, Spectator }

// CameraControl owns transitions; physics consumers only read this state.
public static class GameData
{
	public static int CameraIndex { get; internal set; } = -1;
	public static ControlContext Context { get; internal set; } = ControlContext.Room;
	public static Node3D ActiveVehicle { get; internal set; }
	public static bool InputArmed { get; internal set; }
	public static bool InSpectatorMode => Context != ControlContext.Vehicle;
	public static bool CanDrive(Node3D vehicle) => Context == ControlContext.Vehicle
		&& InputArmed && ActiveVehicle == vehicle;
}
