namespace Game.Contracts;

/// <summary>
/// A band of entities who count each other as their own — the runtime heir of
/// the worldgen faction. Worldgen deliberately bakes factions down into
/// pairwise relationships and then throws the faction away so allegiances can
/// drift; a Party is what allegiance looks like once the world is RUNNING:
/// joinable, leavable, and poachable.
///
/// Like an entity or an item, a party is a describable object carrying the
/// same (axis, valence) sheet everything else does. That is the whole trick
/// behind recruitment: "would I join you?" is the ordinary preference-against-
/// descriptors question the sim already answers about wine bottles, so a party
/// that reads Glory +0.6 appeals to whoever weights Glory — no separate
/// recruitability stat, no second value system to keep in sync.
///
/// The player is not special here. They begin as a party of one, and every
/// NPC forms and poaches parties by the same rules.
/// </summary>
public class Party
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Whoever the party formed around. Also present in MemberIds.</summary>
    public string? LeaderId { get; set; }

    public List<string> MemberIds { get; set; } = new();

    /// <summary>What this party is LIKE — the sheet a prospect weighs its
    /// preferences against. Same shape as Entity.SelfDescriptors.</summary>
    public Dictionary<string, double> Descriptors { get; set; } = new();

    public bool Contains(string entityId) => MemberIds.Contains(entityId);
}
