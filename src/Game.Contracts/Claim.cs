namespace Game.Contracts;

/// <summary>
/// The assertion atom: (object, (axis, valence)). A claim/descriptor/effect/
/// reputation-entry is always axis-keyed, never a bare scalar — the receiver
/// decides what it is worth by crossing it against their own (axis, weight)
/// valuation sheet. Past and present claims on one axis simply sum.
/// </summary>
public record Claim(string ObjectId, string ObjectName, string Axis, double Valence);
