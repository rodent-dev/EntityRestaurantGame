using System.Numerics;
using System.Text.Json;
using Engine.Spatial;
using Game.Contracts;
using Game.Services;
using Game.Services.Actions;
using Game.Services.Effects;

namespace EntityARestaurantGame.World;

/// <summary>
/// The one place the world is assembled — Program (both UIs) and the headless
/// SmokeTest build through here, so their worlds can never drift apart.
/// Locations come from data/world/locations.json, entities from
/// data/entities/*.json (templates/ holds spawnable archetypes). Like the item
/// pipeline, definitions validate at load: unknown actions and axis typos
/// fail immediately, not mid-sim.
/// </summary>
public sealed class WorldBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public LocationRegistry Locations { get; }
    public DialogService Dialog { get; }
    public PartyService Parties { get; }
    public EntityController Controller { get; }

    /// <summary>The generated world, when this builder came from BuildGenerated; null for the handcrafted world.</summary>
    public WorldMap? Map { get; }
    public string DataRoot { get; }

    private readonly ItemFactory _itemFactory;
    private EntityDefinition? _drifterTemplate;
    private int _drifterCount;

    /// <summary>Shared stream for per-entity actions built during Instantiate
    /// (currently Work). Null when the run is deliberately unseeded.</summary>
    private readonly Random? _actionRng;

    /// <summary>The handcrafted world: data/world/locations.json + data/entities/*.json.
    /// <paramref name="simSeed"/> makes the RUN reproducible (worldgen has its own seed).</summary>
    public static WorldBuilder BuildDefault(int? simSeed = null)
    {
        string dataRoot = ResolveDataRoot();
        var locations = LoadLocationDefs(Path.Combine(dataRoot, "world", "locations.json"));
        // Alphabetical for a deterministic decision-stagger order.
        var residents = Directory.GetFiles(Path.Combine(dataRoot, "entities"), "*.json")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(LoadDefinition)
            .ToList();
        return new WorldBuilder(dataRoot, locations, residents, map: null, simSeed);
    }

    /// <summary>A procedurally generated world (data/worldgen pools); the active
    /// settlement becomes the live stage, the rest stay data on <see cref="Map"/>.
    /// <paramref name="seed"/> picks the WORLD; <paramref name="simSeed"/> picks the RUN.</summary>
    public static WorldBuilder BuildGenerated(int? seed = null, int? simSeed = null)
    {
        string dataRoot = ResolveDataRoot();
        var map = WorldGenerator.Generate(dataRoot, seed);
        var stage = map.ActiveStage;
        return new WorldBuilder(dataRoot, stage.Venue, stage.Residents, map, simSeed);
    }

    private WorldBuilder(string dataRoot, IReadOnlyList<LocationDefinition> locationDefs,
        IReadOnlyList<EntityDefinition> residentDefs, WorldMap? map, int? simSeed = null)
    {
        DataRoot = dataRoot;
        Map = map;

        var effects = new EffectRegistry();
        effects.Register("Damage",       new DamageEffect());
        effects.Register("Heal",         new HealEffect());
        effects.Register("Feed",         new FeedEffect());
        effects.Register("Relationship", new RelationshipEffect());
        effects.Register("ConsumeItem",  new ConsumeItemEffect());
        _itemFactory = new ItemFactory(effects);

        Locations = new LocationRegistry();
        foreach (var d in locationDefs)
            Locations.Register(new Location(d.Tag, ToVector(d.Min, d.Tag), ToVector(d.Max, d.Tag)));

        // Each service gets its OWN stream, derived from the one sim seed, so
        // adding an rng call in dialog can't shift what the selector rolls.
        // Null seed = a fresh unpredictable run, exactly as before.
        var seeds = simSeed is { } s ? new Random(s) : null;
        Random? Stream() => seeds is null ? null : new Random(seeds.Next());

        var minds = new MindService();
        Dialog = new DialogService(minds, Stream());
        Parties = new PartyService(Stream());
        Controller = new EntityController(new EntityService(Locations, Stream()), Dialog, Parties);
        _actionRng = Stream();
        Parties.BindWorld(() => Controller.ActiveEntities);

        foreach (var def in residentDefs)
            Controller.AddEntity(Instantiate(def));
    }

    /// <summary>A fresh pawn from the drifter template, known by no one.</summary>
    public Entity SpawnNobody()
    {
        _drifterTemplate ??= LoadDefinition(Path.Combine(DataRoot, "entities", "templates", "drifter.json"));
        _drifterCount++;
        var pawn = Instantiate(_drifterTemplate);
        pawn.ID = $"drifter_{_drifterCount}";
        pawn.DisplayName = $"Drifter {_drifterCount}";
        return pawn;
    }

    private static string ResolveDataRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(dir, "data", "entities")))
            dir = Path.GetDirectoryName(dir)
                  ?? throw new DirectoryNotFoundException($"No data\\entities folder found at or above {AppContext.BaseDirectory}");
        return Path.Combine(dir, "data");
    }

    private static List<LocationDefinition> LoadLocationDefs(string path) =>
        JsonSerializer.Deserialize<List<LocationDefinition>>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"Empty location list: {path}");

    private static EntityDefinition LoadDefinition(string path)
    {
        var def = JsonSerializer.Deserialize<EntityDefinition>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"Empty entity definition: {path}");
        if (string.IsNullOrWhiteSpace(def.Id))
            throw new InvalidDataException($"Entity definition missing 'id': {path}");
        ValidateAxes(def.Mind?.Preferences?.Keys, path);
        ValidateAxes(def.SelfDescriptors?.Keys, path);
        return def;
    }

    private Entity Instantiate(EntityDefinition def)
    {
        var entity = new Entity
        {
            ID = def.Id,
            DisplayName = def.DisplayName,
            EntityType = def.EntityType,
            Position = ToVector(def.Position, def.Id),
            Inventory = [.. def.Inventory.Select(LoadItem)],
        };

        foreach (var b in def.Behaviors)
            entity.Behaviors.Add(new BehaviorModule(b.Name, [.. b.Keywords], b.Affect, b.Scalar));

        foreach (string name in def.Actions)
            entity.Actions.AttachAction(CreateAction(name));

        // Walking and asking-along are universal, so they are attached rather
        // than declared: every entity can always be told to go somewhere, no
        // data file can omit the one verb the reach gate depends on, and bands
        // can form anywhere in the world rather than only around the player.
        // Attaching twice is harmless — ActionModule keys by name.
        entity.Actions.AttachAction(new ApproachAction());
        entity.Actions.AttachAction(new RecruitAction(Parties));

        // A job attaches Work as one more CANDIDATE — a choice weighed against
        // everything else, never an assignment. Silently skipped when none of
        // the job's location tags exist in this venue.
        if (def.Work is { } work)
        {
            var workplace = work.Location
                .Select(tag => Locations.GetByTag(tag))
                .FirstOrDefault(l => l is not null);
            if (workplace is not null)
            {
                foreach (string itemId in work.Produces)
                    if (!File.Exists(Path.Combine(DataRoot, "items", $"{itemId}.json")))
                        throw new InvalidDataException($"Entity '{def.Id}' work.produces references unknown item '{itemId}'.");
                entity.Actions.AttachAction(new WorkAction(
                    new WorkSpec(workplace, work.Produces, work.MaxStock, work.Keywords),
                    LoadItem, _actionRng));
            }
        }

        if (def.Mind is { } mind)
        {
            if (mind.Preferences is not null) entity.Mind.Preferences = new(mind.Preferences);
            if (mind.Skepticism is { } skepticism) entity.Mind.Skepticism = skepticism;
            if (mind.Impulsiveness is { } impulsiveness) entity.Mind.Impulsiveness = impulsiveness;
            if (mind.Sociability is { } sociability) entity.Mind.Sociability = sociability;
        }

        if (def.SelfDescriptors is not null)
            entity.SelfDescriptors = new(def.SelfDescriptors);

        // Stat overrides land after construction, so MaxHealth still derives
        // from the ctor-time Constitution — identical to the old hardcoded
        // setup. (Known quirk, tracked as P2.10 in RECOMMENDATIONS.md.)
        if (def.Stats is { } stats)
        {
            if (stats.Strength is { } str) entity.Stats.Strength = str;
            if (stats.Agility is { } agi) entity.Stats.Agility = agi;
            if (stats.Intelligence is { } intel) entity.Stats.Intelligence = intel;
            if (stats.Constitution is { } con) entity.Stats.Constitution = con;
            if (stats.Wisdom is { } wis) entity.Stats.Wisdom = wis;
            if (stats.Charisma is { } cha) entity.Stats.Charisma = cha;
        }

        if (def.Relationships is not null)
            foreach (var (id, score) in def.Relationships)
                entity.EntityRelationships[id] = score;

        return entity;
    }

    private IItem LoadItem(string id) =>
        _itemFactory.LoadFromFile(Path.Combine(DataRoot, "items", $"{id}.json"));

    private IAction CreateAction(string name) => name switch
    {
        "Attack"   => new AttackAction(),
        "Defend"   => new DefendAction(),
        "Grapple"  => new GrappleAction(),
        "Consume"  => new ConsumeAction(),
        "Enchant"  => new EnchantAction(),
        "Speak"    => new SpeakAction(),
        "Converse" => new ConverseAction(Dialog),
        "Idle"     => new IdleAction(),
        "Approach" => new ApproachAction(),
        "Recruit"  => new RecruitAction(Parties),
        _ => throw new InvalidDataException($"Unknown action '{name}' — add it to WorldBuilder.CreateAction."),
    };

    private static Vector3 ToVector(float[] values, string context) =>
        values.Length == 3
            ? new Vector3(values[0], values[1], values[2])
            : throw new InvalidDataException($"Expected [x, y, z], got {values.Length} numbers ({context})");

    private static void ValidateAxes(IEnumerable<string>? axes, string path)
    {
        if (axes is null) return;
        foreach (string axis in axes)
            if (!Axes.All.Contains(axis))
                throw new InvalidDataException($"Unknown axis '{axis}' in {path} — valid: {string.Join(", ", Axes.All)}");
    }
}
