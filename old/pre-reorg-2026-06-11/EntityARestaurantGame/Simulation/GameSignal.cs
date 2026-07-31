namespace EntityARestaurantGame.Simulation;

/// <summary>
/// A line in the world's announcement log. Events announce what happened;
/// systems (and the player, via the UI) decide what it means.
/// </summary>
public record GameSignal(int Tick, string Message);
