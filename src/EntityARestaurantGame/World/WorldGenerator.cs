using System.Numerics;
using System.Text.Json;
using Game.Contracts;

namespace EntityARestaurantGame.World;

/// <summary>
/// Procedural world generation: pools + knobs + one seeded Random, sampled in
/// a fixed order — same seed, same world, every time. Emits the SAME
/// definition shapes hand-authored content uses (EntityDefinition /
/// LocationDefinition), so generated residents instantiate through the normal
/// validated WorldBuilder path. See specs/WorldGen_Design.md.
/// </summary>
public static class WorldGenerator
{
    public static WorldMap Generate(string dataRoot, int? seedOverride = null)
    {
        string genRoot = Path.Combine(dataRoot, "worldgen");
        var config = LoadJson<WorldGenConfig>(Path.Combine(genRoot, "config.json"));
        var names = LoadJson<NamePools>(Path.Combine(genRoot, "names.json"));
        var venues = LoadJson<List<VenueTemplate>>(Path.Combine(genRoot, "venues.json"));
        var factions = LoadFactions(Path.Combine(genRoot, "factions.json"));
        var archetypes = Directory.GetFiles(Path.Combine(genRoot, "archetypes"), "*.json")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(LoadArchetype)
            .ToList();

        if (venues.Count == 0) throw new InvalidDataException("venues.json has no venue templates.");
        if (archetypes.Count == 0) throw new InvalidDataException("No archetypes in data/worldgen/archetypes/.");
        ValidateIntRange(config.SettlementsPerRegion, "config.settlementsPerRegion");

        int seed = seedOverride ?? config.Seed ?? Random.Shared.Next(1, 1_000_000);
        var rng = new Random(seed);

        var regionNames = ShuffledNames(rng, names.Regions, config.RegionCount, "Region");
        var usedSettlementNames = new HashSet<string>();
        var regions = new List<RegionSite>();

        for (int r = 0; r < config.RegionCount; r++)
        {
            var size = new Vector2(config.RegionSize[0], config.RegionSize[1]);
            int settlementCount = SampleInt(rng, config.SettlementsPerRegion);
            var settlements = new List<SettlementSite>();

            for (int s = 0; s < settlementCount; s++)
                settlements.Add(GenerateSettlement(rng, names, venues, archetypes, factions, usedSettlementNames, size));

            regions.Add(new RegionSite { Name = regionNames[r], Size = size, Settlements = settlements });
        }

        return new WorldMap { Seed = seed, Regions = regions };
    }

    private static SettlementSite GenerateSettlement(Random rng, NamePools names,
        List<VenueTemplate> venues, List<ArchetypeDefinition> archetypes,
        Dictionary<string, Dictionary<string, int[]>> factions,
        HashSet<string> usedNames, Vector2 regionSize)
    {
        string name = UniqueSettlementName(rng, names, usedNames);
        var venue = venues[rng.Next(venues.Count)];
        var mapPos = new Vector2(
            (float)(rng.NextDouble() * regionSize.X),
            (float)(rng.NextDouble() * regionSize.Y));

        var firstNames = Shuffle(rng, names.FirstNames);
        int nameCursor = 0;
        var residents = new List<(EntityDefinition Def, string Faction)>();
        string slug = Slug(name);

        foreach (var archetype in archetypes)
        {
            // Habitat tag picks the spawn zone (wolves in the wilds); default
            // is the venue interior — the template's first location.
            var spawnZone = archetype.Habitat is { } habitat
                ? venue.Locations.FirstOrDefault(l => l.Tag == habitat) ?? venue.Locations[0]
                : venue.Locations[0];

            int count = SampleInt(rng, archetype.PerSettlement);
            for (int i = 0; i < count; i++)
            {
                string display = firstNames.Count == 0 ? $"{archetype.Type} {i + 1}"
                    : firstNames[nameCursor % firstNames.Count] + (nameCursor >= firstNames.Count ? $" {nameCursor / firstNames.Count + 1}" : "");
                nameCursor++;
                residents.Add((SampleResident(rng, archetype, spawnZone,
                    id: $"{slug}_{archetype.Type.ToLowerInvariant()}_{i + 1}",
                    displayName: display), archetype.Faction));
            }
        }

        // Faction identity materialized as pairwise standing: each direction
        // sampled independently from the stance matrix (grudges aren't
        // mutual), plus a point of personal noise. Dialog keeps moving these
        // afterward — factions drift, betrayal stays possible.
        foreach (var (def, faction) in residents)
            foreach (var (other, otherFaction) in residents)
            {
                if (ReferenceEquals(def, other)) continue;
                if (!factions.TryGetValue(faction, out var stances) ||
                    !stances.TryGetValue(otherFaction, out var range)) continue;
                int rel = SampleInt(rng, range) + rng.Next(-1, 2);
                if (rel == 0) continue;
                def.Relationships ??= [];
                def.Relationships[other.Id] = rel;
            }

        return new SettlementSite
        {
            Name = name,
            VenueName = venue.Name,
            MapPosition = mapPos,
            Venue = venue.Locations,
            Residents = [.. residents.Select(r => r.Def)],
        };
    }

    private static EntityDefinition SampleResident(Random rng, ArchetypeDefinition archetype,
        LocationDefinition outerBounds, string id, string displayName)
    {
        float x = Lerp(outerBounds.Min[0] + 1, outerBounds.Max[0] - 1, rng);
        float y = Lerp(outerBounds.Min[1] + 1, outerBounds.Max[1] - 1, rng);

        var inventory = new List<string>(archetype.Inventory.Always);
        int picks = SampleInt(rng, archetype.Inventory.PickCount);
        inventory.AddRange(Shuffle(rng, archetype.Inventory.Pool).Take(picks));

        MindDefinition? mind = null;
        if (archetype.Mind is { } m)
        {
            mind = new MindDefinition
            {
                Preferences = m.Preferences?.ToDictionary(kv => kv.Key, kv => SampleDouble(rng, kv.Value)),
                Skepticism = m.Skepticism is { } sk ? SampleDouble(rng, sk) : null,
                Impulsiveness = m.Impulsiveness is { } im ? SampleDouble(rng, im) : null,
                Sociability = m.Sociability is { } so ? SampleDouble(rng, so) : null,
            };
        }

        StatsDefinition? stats = null;
        if (archetype.Stats is { } st)
        {
            stats = new StatsDefinition();
            foreach (var (key, range) in st)
            {
                int value = SampleInt(rng, range);
                switch (key.ToLowerInvariant())
                {
                    case "strength": stats.Strength = value; break;
                    case "agility": stats.Agility = value; break;
                    case "intelligence": stats.Intelligence = value; break;
                    case "constitution": stats.Constitution = value; break;
                    case "wisdom": stats.Wisdom = value; break;
                    case "charisma": stats.Charisma = value; break;
                    default: throw new InvalidDataException($"Unknown stat '{key}' in archetype '{archetype.Type}'.");
                }
            }
        }

        return new EntityDefinition
        {
            Id = id,
            DisplayName = displayName,
            EntityType = archetype.Type,
            Position = [x, y, 0],
            Inventory = inventory,
            Behaviors = [.. archetype.Behaviors.Select(b => new BehaviorDefinition
            {
                Name = b.Name,
                Keywords = b.Keywords,
                Affect = SampleInt(rng, b.Affect),
            })],
            Actions = archetype.Actions,
            Work = archetype.Work,
            Mind = mind,
            SelfDescriptors = archetype.SelfDescriptors?.ToDictionary(kv => kv.Key, kv => SampleDouble(rng, kv.Value)),
            Stats = stats,
        };
    }

    /// <summary>Write the generated world as ordinary content files —
    /// locations.json + entities/*.json per settlement — so a good seed can be
    /// promoted to hand-authored content by copying files. Returns the folder.</summary>
    public static string Dump(WorldMap map, string dataRoot)
    {
        var writeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        string root = Path.Combine(dataRoot, "generated", map.Seed.ToString());
        foreach (var region in map.Regions)
            foreach (var s in region.Settlements)
            {
                string dir = Path.Combine(root, Slug(region.Name), Slug(s.Name));
                Directory.CreateDirectory(Path.Combine(dir, "entities"));
                File.WriteAllText(Path.Combine(dir, "locations.json"),
                    JsonSerializer.Serialize(s.Venue, writeOptions));
                foreach (var resident in s.Residents)
                    File.WriteAllText(Path.Combine(dir, "entities", $"{resident.Id}.json"),
                        JsonSerializer.Serialize(resident, writeOptions));
            }
        return root;
    }

    // ── Pool loading + validation ───────────────────────────────────────────

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static T LoadJson<T>(string path) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path), ReadOptions)
            ?? throw new InvalidDataException($"Empty or invalid JSON: {path}");

    private static Dictionary<string, Dictionary<string, int[]>> LoadFactions(string path)
    {
        var matrix = LoadJson<Dictionary<string, Dictionary<string, int[]>>>(path);
        foreach (var (faction, stances) in matrix)
            foreach (var (other, range) in stances)
                ValidateIntRange(range, $"{path}: {faction} → {other}");
        return matrix;
    }

    private static ArchetypeDefinition LoadArchetype(string path)
    {
        var a = LoadJson<ArchetypeDefinition>(path);
        if (string.IsNullOrWhiteSpace(a.Type))
            throw new InvalidDataException($"Archetype missing 'type': {path}");
        ValidateIntRange(a.PerSettlement, $"{path}: perSettlement");
        foreach (var b in a.Behaviors) ValidateIntRange(b.Affect, $"{path}: behavior '{b.Name}' affect");
        if (a.Mind is { } m)
        {
            if (m.Preferences is not null)
                foreach (var (axis, range) in m.Preferences)
                {
                    ValidateAxis(axis, path);
                    ValidateDoubleRange(range, $"{path}: preference '{axis}'");
                }
            if (m.Skepticism is { } sk) ValidateDoubleRange(sk, $"{path}: skepticism");
            if (m.Impulsiveness is { } im) ValidateDoubleRange(im, $"{path}: impulsiveness");
            if (m.Sociability is { } so) ValidateDoubleRange(so, $"{path}: sociability");
        }
        if (a.SelfDescriptors is not null)
            foreach (var (axis, range) in a.SelfDescriptors)
            {
                ValidateAxis(axis, path);
                ValidateDoubleRange(range, $"{path}: selfDescriptor '{axis}'");
            }
        if (a.Stats is not null)
            foreach (var (key, range) in a.Stats) ValidateIntRange(range, $"{path}: stat '{key}'");
        ValidateIntRange(a.Inventory.PickCount, $"{path}: inventory.pickCount");
        if (a.Inventory.PickCount[1] > a.Inventory.Pool.Count)
            throw new InvalidDataException($"{path}: inventory.pickCount max exceeds pool size {a.Inventory.Pool.Count}.");
        return a;
    }

    private static void ValidateAxis(string axis, string context)
    {
        if (!Axes.All.Contains(axis))
            throw new InvalidDataException($"Unknown axis '{axis}' in {context} — valid: {string.Join(", ", Axes.All)}");
    }

    private static void ValidateIntRange(int[] r, string context)
    {
        if (r.Length != 2 || r[0] > r[1])
            throw new InvalidDataException($"{context}: expected [min, max] with min <= max.");
    }

    private static void ValidateDoubleRange(double[] r, string context)
    {
        if (r.Length != 2 || r[0] > r[1])
            throw new InvalidDataException($"{context}: expected [min, max] with min <= max.");
    }

    // ── Sampling ────────────────────────────────────────────────────────────

    private static int SampleInt(Random rng, int[] range) => rng.Next(range[0], range[1] + 1);
    private static double SampleDouble(Random rng, double[] range) => range[0] + rng.NextDouble() * (range[1] - range[0]);
    private static float Lerp(float min, float max, Random rng) => min + (float)rng.NextDouble() * Math.Max(0, max - min);

    private static List<string> Shuffle(Random rng, List<string> source)
    {
        var list = new List<string>(source);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    private static List<string> ShuffledNames(Random rng, List<string> pool, int count, string fallback)
    {
        var shuffled = Shuffle(rng, pool);
        var result = new List<string>();
        for (int i = 0; i < count; i++)
            result.Add(shuffled.Count == 0 ? $"{fallback} {i + 1}"
                : i < shuffled.Count ? shuffled[i]
                : $"{shuffled[i % shuffled.Count]} {i / shuffled.Count + 1}");
        return result;
    }

    private static string UniqueSettlementName(Random rng, NamePools names, HashSet<string> used)
    {
        for (int attempt = 0; attempt < 60; attempt++)
        {
            string candidate =
                names.SettlementPrefixes[rng.Next(names.SettlementPrefixes.Count)] +
                names.SettlementSuffixes[rng.Next(names.SettlementSuffixes.Count)];
            if (used.Add(candidate)) return candidate;
        }
        string numbered = $"Settlement {used.Count + 1}";
        used.Add(numbered);
        return numbered;
    }

    private static string Slug(string name) =>
        new([.. name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_')]);
}
