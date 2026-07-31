using System.Text.Json.Serialization;

namespace EntityARestaurantGame.World;

// JSON DTOs for data/world/ and data/entities/. Same rules as the item
// pipeline: case-insensitive keys, trailing commas and comments allowed,
// unknown keys are a load-time error (typos fail loudly). Nullable members
// mean "not specified — keep the Contracts default".

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LocationDefinition
{
    public string Tag { get; set; } = string.Empty;
    public float[] Min { get; set; } = [];
    public float[] Max { get; set; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class BehaviorDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = [];
    public int Affect { get; set; }
    public int Scalar { get; set; } = 1;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class MindDefinition
{
    public Dictionary<string, double>? Preferences { get; set; }
    public double? Skepticism { get; set; }
    public double? Impulsiveness { get; set; }
    public double? Sociability { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class StatsDefinition
{
    public int? Strength { get; set; }
    public int? Agility { get; set; }
    public int? Intelligence { get; set; }
    public int? Constitution { get; set; }
    public int? Wisdom { get; set; }
    public int? Charisma { get; set; }
}

/// <summary>A job: where it happens (first tag present in the venue wins),
/// what it makes, when the worker downs tools, and which natures it suits.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class WorkDefinition
{
    public List<string> Location { get; set; } = [];
    public List<string> Produces { get; set; } = [];
    public int MaxStock { get; set; } = 4;
    public List<string> Keywords { get; set; } = ["Industrious"];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EntityDefinition
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public float[] Position { get; set; } = [0, 0, 0];
    public List<string> Inventory { get; set; } = [];
    public List<BehaviorDefinition> Behaviors { get; set; } = [];
    public List<string> Actions { get; set; } = [];
    public WorkDefinition? Work { get; set; }
    public MindDefinition? Mind { get; set; }
    public Dictionary<string, double>? SelfDescriptors { get; set; }
    public StatsDefinition? Stats { get; set; }
    public Dictionary<string, int>? Relationships { get; set; }
}
