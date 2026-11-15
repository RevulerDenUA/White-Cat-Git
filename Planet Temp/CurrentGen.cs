using Godot;

public partial class CurrentGen : Node
{
    // [1] Holds all structure parameters of the planet the player entered
    public static PlanetData ActivePlanet;
    public static bool HasActivePlanet = false;

    // [2] Saves the planet data before unloading the solar system
    public static void SavePlanetForTransition(PlanetData d)
    {
        ActivePlanet = d;
        HasActivePlanet = true;

        GD.Print($"[CurrentGen] Loaded Planet #{d.Order} | Type: {d.Type} | Gravity: {d.Gravity} | Temp: {d.Temperature}");
    }



}