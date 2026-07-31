using System.Numerics;
using System.Text;

namespace EntityARestaurantGame.World;

/// <summary>
/// A generated settlement: a venue layout (LocationDefinitions) plus resident
/// EntityDefinitions — the same shapes hand-authored content uses, so the
/// active one instantiates through the normal WorldBuilder path and the rest
/// stay dormant data for the future world controller.
/// </summary>
public sealed class SettlementSite
{
    public required string Name { get; init; }
    public required string VenueName { get; init; }
    public Vector2 MapPosition { get; init; }
    public required List<LocationDefinition> Venue { get; init; }
    public required List<EntityDefinition> Residents { get; init; }
}

public sealed class RegionSite
{
    public required string Name { get; init; }
    public Vector2 Size { get; init; }
    public required List<SettlementSite> Settlements { get; init; }
}

/// <summary>The generated world as data. One settlement is the live stage;
/// everything else waits for parties/travel/dormancy (see WorldGen_Design.md).</summary>
public sealed class WorldMap
{
    public required int Seed { get; init; }
    public required List<RegionSite> Regions { get; init; }

    /// <summary>The settlement the sim actually runs — first generated, deterministic.</summary>
    public SettlementSite ActiveStage => Regions[0].Settlements[0];

    public int SettlementCount => Regions.Sum(r => r.Settlements.Count);
    public int ResidentCount => Regions.Sum(r => r.Settlements.Sum(s => s.Residents.Count));

    public string Summary()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[gen] seed {Seed} — {Regions.Count} regions, {SettlementCount} settlements, {ResidentCount} residents");
        foreach (var region in Regions)
        {
            sb.AppendLine($"[gen] {region.Name} ({region.Size.X:0}x{region.Size.Y:0})");
            foreach (var s in region.Settlements)
            {
                var byType = s.Residents.GroupBy(r => r.EntityType)
                    .Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key);
                sb.AppendLine($"[gen]   {s.Name} ({s.VenueName}) @ ({s.MapPosition.X:0},{s.MapPosition.Y:0}) — {string.Join(", ", byType)}");
            }
        }
        sb.Append($"[gen] active stage: {ActiveStage.Name}");
        return sb.ToString();
    }
}
