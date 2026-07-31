using System.Text.Json.Serialization;

namespace EntityARestaurantGame.World;

// JSON DTOs for data/worldgen/. Ranges are two-element [min, max] arrays,
// inclusive; equal ends mean "fixed". Same strictness as everything else:
// unknown keys are a load-time error.

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class WorldGenConfig
{
    public int? Seed { get; set; }
    public int RegionCount { get; set; } = 3;
    public float[] RegionSize { get; set; } = [60, 60];
    public int[] SettlementsPerRegion { get; set; } = [2, 4];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class NamePools
{
    public List<string> Regions { get; set; } = [];
    public List<string> SettlementPrefixes { get; set; } = [];
    public List<string> SettlementSuffixes { get; set; } = [];
    public List<string> FirstNames { get; set; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VenueTemplate
{
    public string Name { get; set; } = string.Empty;
    public List<LocationDefinition> Locations { get; set; } = [];
}

// ── Archetypes: EntityDefinition with ranges where numbers would be ────────

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ArchetypeDefinition
{
    public string Type { get; set; } = string.Empty;
    /// <summary>Faction membership — pairs with factions.json to materialize
    /// relationships at generation.</summary>
    public string Faction { get; set; } = "settlers";
    /// <summary>Location tag to spawn in; null = the venue interior (first location).</summary>
    public string? Habitat { get; set; }
    public int[] PerSettlement { get; set; } = [1, 1];
    public List<BehaviorRange> Behaviors { get; set; } = [];
    public List<string> Actions { get; set; } = [];
    /// <summary>Passed through to each sampled resident unchanged.</summary>
    public WorkDefinition? Work { get; set; }
    public MindRange? Mind { get; set; }
    public Dictionary<string, double[]>? SelfDescriptors { get; set; }
    public Dictionary<string, int[]>? Stats { get; set; }
    public InventorySpec Inventory { get; set; } = new();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class BehaviorRange
{
    public string Name { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = [];
    public int[] Affect { get; set; } = [1, 1];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class MindRange
{
    public Dictionary<string, double[]>? Preferences { get; set; }
    public double[]? Skepticism { get; set; }
    public double[]? Impulsiveness { get; set; }
    public double[]? Sociability { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class InventorySpec
{
    public List<string> Always { get; set; } = [];
    public int[] PickCount { get; set; } = [0, 0];
    public List<string> Pool { get; set; } = [];
}
