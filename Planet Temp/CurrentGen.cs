using Godot;

public partial class CurrentGen : Node
{
 
    public static PlanetData ActivePlanet;
    public static bool HasActivePlanet = false;

    // Saves the planet data 
    public static void SavePlanetForTransition(PlanetData d)
    {
        ActivePlanet = d;
        HasActivePlanet = true;

        GD.Print($"[CurrentGen] Loaded Planet #{d.Order} | Type: {d.Type} | Gravity: {d.Gravity} | Temp: {d.Temperature}");
    }



}