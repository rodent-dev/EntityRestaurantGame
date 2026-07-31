namespace Game.Contracts;

/// <summary>
/// A line in the world's announcement log. Events announce what happened;
/// systems (and the player, via the UI) decide what it means.
/// </summary>
public record GameSignal(int Tick, string Message);

/// <summary>
/// A line in one entity's PERSONAL log (the RimWorld-style dossier).
/// AsActor marks the entry's protagonist. Loud is the broadcast tier: distinct,
/// value-changing events (speech, deals, violence, deaths) that earn a speech
/// bubble — quiet entries (routine decisions, movement, mind-internal nudges)
/// surface only when the player inspects the entity.
/// </summary>
public record EntityLogEntry(int Tick, string Message, bool AsActor, bool Loud);
