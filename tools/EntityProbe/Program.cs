// Entity probe: one entity, every beat, in full.
//
// The sibling of tools/DecisionProbe, asking the opposite question. That one is
// a POPULATION instrument — thousands of ticks of everybody, reported as
// distributions, good for "hostility holds 12% of the mass and shouldn't".
// This one is a MICROSCOPE: it follows a single entity and records the complete
// field it chose from on each of its beats, so you can stand at one moment and
// ask why this and not that.
//
//   dotnet run --project tools\EntityProbe                       defaults
//   dotnet run --project tools\EntityProbe -- --list             who is on stage
//   dotnet run --project tools\EntityProbe -- 42 2000 1 --focus Bram
//   dotnet run --project tools\EntityProbe -- --focus cook --beats 300
//
// Writes probe.html — a self-contained viewer with the run baked in. Open it.
//
// ── The rule this tool is built around ──────────────────────────────────────
//
// It contains NO copy of any sim rule. Every number it reports either comes off
// the DecisionTrace the controller hands out, or from calling the same function
// the sim called (EntityService.SelectionMass, EntityService.ReachDistance,
// EntityService.Focus). That is deliberate, and it is the whole difference
// between a probe and a fork: a probe that re-derives "how selection works" is
// correct exactly until the day selection changes, and then it lies to you
// confidently. If you need something here that the sim knows and the trace does
// not carry, widen the trace — never re-implement the rule.
using System.Text.Json;
using System.Text.Json.Serialization;
using EntityARestaurantGame;
using EntityARestaurantGame.World;
using Game.Contracts;
using Game.Services;

// ── Arguments ───────────────────────────────────────────────────────────────

var positional = args.Where(a => !a.StartsWith("--")).ToList();
string? Flag(string name)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[i + 1] : null;
}

int worldSeed = positional.Count > 0 && int.TryParse(positional[0], out int w) ? w : 42;
int ticks     = positional.Count > 1 && int.TryParse(positional[1], out int t) ? t : 1500;
int simSeed   = positional.Count > 2 && int.TryParse(positional[2], out int s) ? s : 1;
string? focusArg = Flag("--focus");
int beatCap   = int.TryParse(Flag("--beats"), out int b) ? b : 400;
string outPath = Flag("--out") ?? "probe.html";

var world = WorldBuilder.BuildGenerated(worldSeed, simSeed);
var controller = world.Controller;

if (args.Contains("--list"))
{
    Console.WriteLine($"world seed {worldSeed} · stage '{world.Map!.ActiveStage.Name}'");
    foreach (var e in controller.ActiveEntities)
        Console.WriteLine($"  {e.ID,-24} {e.DisplayName,-18} {e.EntityType,-10} "
                        + $"{e.Actions.GetAllActions().Count()} verbs, "
                        + $"{e.Behaviors.Count} behaviors");
    return;
}

// Resolve the focus by id, display name or type — whichever the user typed.
var focus = focusArg is null
    ? PickInteresting(controller)
    : controller.ActiveEntities.FirstOrDefault(e =>
          e.ID.Equals(focusArg, StringComparison.OrdinalIgnoreCase)
       || e.DisplayName.Contains(focusArg, StringComparison.OrdinalIgnoreCase)
       || e.EntityType.Equals(focusArg, StringComparison.OrdinalIgnoreCase));

if (focus is null)
{
    Console.Error.WriteLine($"no entity matched '{focusArg}'. Try --list.");
    return;
}

Console.WriteLine($"world {worldSeed} · sim {simSeed} · {ticks} ticks · "
                + $"following {focus.DisplayName} ({focus.EntityType}) on '{world.Map!.ActiveStage.Name}'");

// ── Record ──────────────────────────────────────────────────────────────────

string focusId = focus.ID;
var beats = new List<Beat>();
var names = controller.ActiveEntities.ToDictionary(e => e.ID, e => e.DisplayName);

controller.DecisionObserver = tr =>
{
    if (tr.Actor.ID != focusId || beats.Count >= beatCap) return;
    beats.Add(Capture(tr, controller, names));
};

while (controller.CurrentTick < ticks && beats.Count < beatCap)
    controller.Pump(2.5);

if (beats.Count == 0)
{
    Console.Error.WriteLine("no beats recorded — the entity may have died before deciding.");
    return;
}

// ── Emit ────────────────────────────────────────────────────────────────────

var report = new ProbeReport
{
    WorldSeed = worldSeed,
    SimSeed = simSeed,
    Ticks = ticks,
    Stage = world.Map!.ActiveStage.Name,
    Population = controller.ActiveEntities.Count,
    Focus = new FocusInfo
    {
        Id = focus.ID,
        Name = focus.DisplayName,
        Type = focus.EntityType,
        Alive = focus.IsAlive,
        Verbs = [.. focus.Actions.GetAllActions()
            .Select(a => new VerbInfo
            {
                Name = a.ActionName,
                Keywords = [.. a.Keywords.OrderBy(k => k, StringComparer.Ordinal)],
                Duration = a.Duration,
                BaseWeight = a.BaseWeight,
                RequiresItem = a.RequiresItem,
            })
            .OrderBy(v => v.Name, StringComparer.Ordinal)],
        Behaviors = [.. focus.Behaviors
            .Select(x => new BehaviorInfo
            {
                Name = x.Name,
                Keywords = [.. x.Keywords.OrderBy(k => k, StringComparer.Ordinal)],
                Affect = x.Affect,
                Scalar = x.Scalar,
            })
            .OrderBy(x => x.Name, StringComparer.Ordinal)],
    },
    // Named off the enum so the viewer can lay out a lane per source without
    // knowing them — add a source to the sim and it appears here for free.
    Sources = [.. Enum.GetNames<ActionWeightSource>()],
    Beats = beats,
};

var json = JsonSerializer.Serialize(report, ProbeJson.Options);

string viewer = Path.Combine(AppContext.BaseDirectory, "viewer.html");
if (File.Exists(viewer))
{
    File.WriteAllText(outPath,
        File.ReadAllText(viewer).Replace("\"__PROBE_DATA__\"", json));
    Console.WriteLine($"wrote {outPath}  ({beats.Count} beats, {new FileInfo(outPath).Length / 1024} KB)");
}
else
{
    string fallback = Path.ChangeExtension(outPath, ".json");
    File.WriteAllText(fallback, json);
    Console.WriteLine($"viewer.html missing; wrote raw data to {fallback}");
}

Summarize(report);

// ── Picking a subject ───────────────────────────────────────────────────────

// The entity with the most verbs and traits has the widest field to choose
// from, which makes it the one whose decisions are worth watching.
static Entity PickInteresting(EntityController c) =>
    c.ActiveEntities
     .OrderByDescending(e => e.Actions.GetAllActions().Count() + e.Behaviors.Count)
     .ThenBy(e => e.ID, StringComparer.Ordinal)
     .First();

// ── Capture: everything about one beat, none of it re-derived ───────────────

static Beat Capture(DecisionTrace tr, EntityController controller,
    Dictionary<string, string> names)
{
    var actor = tr.Actor;

    // The pool the selector actually rolls over. SelectionMass is the SIM's
    // function, not a copy of it — see the header.
    int poolMass = tr.Candidates.Sum(EntityService.SelectionMass);

    var cands = new List<Cand>(tr.Candidates.Count);
    foreach (var c in tr.Candidates)
    {
        int mass = EntityService.SelectionMass(c);
        float gap = c.Target.ID == actor.ID
            ? 0
            : Math.Max(0, System.Numerics.Vector3.Distance(actor.Position, c.Target.Position)
                          - EntityService.ReachDistance(actor, c.Target));

        cands.Add(new Cand
        {
            Action = c.Action.ActionName,
            Target = names.GetValueOrDefault(c.Target.ID, c.Target.DisplayName),
            TargetId = c.Target.ID,
            Self = c.Target.ID == actor.ID,
            Keywords = [.. c.Action.Keywords.OrderBy(k => k, StringComparer.Ordinal)],
            Weights = [.. c.Weights.Select(x => new W { Source = x.Source.ToString(), Value = x.Weight })],
            Total = c.TotalWeight,
            Mass = mass,
            Share = poolMass == 0 ? 0 : Math.Round(mass * 100.0 / poolMass, 3),
            Floored = c.TotalWeight <= 0,
            Gap = MathF.Round(gap, 2),
            Won = ReferenceEquals(c, tr.Selected),
        });
    }

    // A carried-on beat's Selected is the synthetic incumbent candidate, so it
    // is not in the field and no row is marked won. Say so, rather than
    // pretending one of the challengers was chosen.
    bool winnerInField = cands.Any(c => c.Won);

    return new Beat
    {
        Tick = tr.Tick,
        Outcome = tr.Outcome.ToString(),
        Hp = Math.Round(actor.GetCurrentHealth(), 1),
        HpMax = Math.Round(actor.GetMaxHealth(), 1),
        Hunger = actor.Needs.Hunger,
        Mood = actor.Needs.Mood,
        FocusLevel = Math.Round(EntityService.Focus(actor), 3),
        X = MathF.Round(actor.Position.X, 2),
        Y = MathF.Round(actor.Position.Y, 2),
        Where = controller.LocationTag(actor),
        PoolMass = poolMass,
        FloorMass = cands.Where(c => c.Floored).Sum(c => c.Mass),
        WinnerAction = tr.Selected.Action.ActionName,
        WinnerTarget = names.GetValueOrDefault(tr.Selected.Target.ID, tr.Selected.Target.DisplayName),
        WinnerWeight = tr.Selected.TotalWeight,
        WinnerInField = winnerInField,
        Item = tr.Item?.ItemName,
        Current = actor.Current is { } a
            ? new Busy
            {
                Action = a.ActionName,
                Target = names.GetValueOrDefault(a.TargetId, a.TargetId),
                Elapsed = a.Elapsed,
                Duration = actor.Actions.GetAction(a.ActionName)?.Duration ?? 1,
                Progress = Math.Round(a.Progress, 2),
                Influenced = a.Influenced,
            }
            : null,
        Commitment = tr.Commitment is { } com
            ? new Com
            {
                Total = com.Total,
                Held = tr.Outcome == BeatOutcome.CarriedOn,
                Terms = [.. com.Terms.Select(x => new W { Source = x.Reason, Value = x.Weight })],
            }
            : null,
        Influence = actor.PendingInfluence is { } inf
            ? new Inf
            {
                Label = inf.Label,
                Keywords = [.. inf.Keywords],
                Target = inf.TargetEntityId is { } id ? names.GetValueOrDefault(id, id) : null,
                Strength = inf.Strength,
            }
            : null,
        Candidates = cands,
    };
}

// ── A terminal summary, so the tool is useful without opening anything ──────

static void Summarize(ProbeReport r)
{
    var o = new System.Text.StringBuilder();
    void Rule(string title) =>
        o.AppendLine($"\n── {title} " + new string('─', Math.Max(0, 62 - title.Length)));

    long totalMass = r.Beats.Sum(x => (long)x.PoolMass);
    long floorMass = r.Beats.Sum(x => (long)x.FloorMass);

    Rule($"{r.Focus.Name} — {r.Beats.Count} beats");
    o.AppendLine($"  field size   {r.Beats.Average(x => x.Candidates.Count),8:0.0} candidates per beat");
    o.AppendLine($"  floor share  {floorMass * 100.0 / Math.Max(1, totalMass),8:0.0}% of this entity's probability sat on");
    o.AppendLine( "               options the pipeline scored at zero or below");

    Rule("WHAT ITS BEATS BECAME");
    foreach (var (outcome, n) in r.Beats.GroupBy(x => x.Outcome)
                 .Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2))
        o.AppendLine($"  {outcome,-14} {n,6} {n * 100.0 / r.Beats.Count,7:0.0}%");

    Rule("WHAT IT CHOSE");
    foreach (var (name, n) in r.Beats.GroupBy(x => x.WinnerAction)
                 .Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2))
        o.AppendLine($"  {name,-14} {n,6} {n * 100.0 / r.Beats.Count,7:0.0}%");

    Rule("WHAT EVER MOVED IT — weight by source, across every candidate");
    var bySource = r.Beats
        .SelectMany(x => x.Candidates)
        .SelectMany(c => c.Weights)
        .GroupBy(x => x.Source)
        .Select(g => (Source: g.Key, Total: g.Sum(x => (long)x.Value), Count: g.LongCount()))
        .ToList();
    o.AppendLine($"  {"source",-16} {"applied",10} {"sum",12} {"mean",8}");
    foreach (var src in r.Sources)
    {
        // Commitment is not a lane of this table and never can be: it is priced
        // against the field, never inside it. Counting it here as "never
        // applied" would report the one rule the design is most insistent about
        // as a dead system. It gets its own line below.
        if (src == nameof(ActionWeightSource.Commitment)) continue;

        var hit = bySource.FirstOrDefault(x => x.Source == src);
        // A source that never fired is the interesting row: a lane of judgement
        // this entity has, and never uses.
        o.AppendLine(hit.Count == 0
            ? $"  {src,-16} {"—",10}  never applied"
            : $"  {src,-16} {hit.Count,10:n0} {hit.Total,12:n0} {hit.Total / (double)hit.Count,8:0.0}");
    }

    var weighed = r.Beats.Where(x => x.Commitment is not null).Select(x => x.Commitment!).ToList();
    o.AppendLine($"  {"Commitment",-16} {weighed.Count,10:n0} "
        + (weighed.Count == 0
            ? "  never weighed (nothing this entity does takes more than a beat)"
            : $"{weighed.Sum(x => (long)x.Total),12:n0} {weighed.Average(x => x.Total),8:0.0}"
            + $"   held {weighed.Count(x => x.Held)}, walked out {weighed.Count(x => !x.Held)}"));

    Console.WriteLine(o.ToString());
}

// ── Wire format ─────────────────────────────────────────────────────────────
// Short names: this gets embedded in the HTML, and a 400-beat run with thirty
// candidates a beat is a great many repeated keys.

static class ProbeJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

sealed class ProbeReport
{
    [JsonPropertyName("worldSeed")] public int WorldSeed { get; set; }
    [JsonPropertyName("simSeed")] public int SimSeed { get; set; }
    [JsonPropertyName("ticks")] public int Ticks { get; set; }
    [JsonPropertyName("stage")] public string Stage { get; set; } = "";
    [JsonPropertyName("population")] public int Population { get; set; }
    [JsonPropertyName("focus")] public FocusInfo Focus { get; set; } = new();
    [JsonPropertyName("sources")] public List<string> Sources { get; set; } = [];
    [JsonPropertyName("beats")] public List<Beat> Beats { get; set; } = [];
}

sealed class FocusInfo
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("alive")] public bool Alive { get; set; }
    [JsonPropertyName("verbs")] public List<VerbInfo> Verbs { get; set; } = [];
    [JsonPropertyName("behaviors")] public List<BehaviorInfo> Behaviors { get; set; } = [];
}

sealed class VerbInfo
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("kw")] public List<string> Keywords { get; set; } = [];
    [JsonPropertyName("dur")] public int Duration { get; set; }
    [JsonPropertyName("base")] public int BaseWeight { get; set; }
    [JsonPropertyName("item")] public bool RequiresItem { get; set; }
}

sealed class BehaviorInfo
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("kw")] public List<string> Keywords { get; set; } = [];
    [JsonPropertyName("affect")] public int Affect { get; set; }
    [JsonPropertyName("scalar")] public int Scalar { get; set; }
}

sealed class Beat
{
    [JsonPropertyName("t")] public int Tick { get; set; }
    [JsonPropertyName("out")] public string Outcome { get; set; } = "";
    [JsonPropertyName("hp")] public double Hp { get; set; }
    [JsonPropertyName("hpMax")] public double HpMax { get; set; }
    [JsonPropertyName("hunger")] public int Hunger { get; set; }
    [JsonPropertyName("mood")] public int Mood { get; set; }
    [JsonPropertyName("focus")] public double FocusLevel { get; set; }
    [JsonPropertyName("x")] public float X { get; set; }
    [JsonPropertyName("y")] public float Y { get; set; }
    [JsonPropertyName("where")] public string Where { get; set; } = "";
    [JsonPropertyName("pool")] public int PoolMass { get; set; }
    [JsonPropertyName("floorMass")] public int FloorMass { get; set; }
    [JsonPropertyName("wAction")] public string WinnerAction { get; set; } = "";
    [JsonPropertyName("wTarget")] public string WinnerTarget { get; set; } = "";
    [JsonPropertyName("wWeight")] public int WinnerWeight { get; set; }
    [JsonPropertyName("inField")] public bool WinnerInField { get; set; }
    [JsonPropertyName("item")] public string? Item { get; set; }
    [JsonPropertyName("busy")] public Busy? Current { get; set; }
    [JsonPropertyName("com")] public Com? Commitment { get; set; }
    [JsonPropertyName("inf")] public Inf? Influence { get; set; }
    [JsonPropertyName("c")] public List<Cand> Candidates { get; set; } = [];
}

sealed class Cand
{
    [JsonPropertyName("a")] public string Action { get; set; } = "";
    [JsonPropertyName("tg")] public string Target { get; set; } = "";
    [JsonPropertyName("tid")] public string TargetId { get; set; } = "";
    [JsonPropertyName("self")] public bool Self { get; set; }
    [JsonPropertyName("kw")] public List<string> Keywords { get; set; } = [];
    [JsonPropertyName("w")] public List<W> Weights { get; set; } = [];
    [JsonPropertyName("tot")] public int Total { get; set; }
    [JsonPropertyName("mass")] public int Mass { get; set; }
    [JsonPropertyName("share")] public double Share { get; set; }
    [JsonPropertyName("floored")] public bool Floored { get; set; }
    [JsonPropertyName("gap")] public float Gap { get; set; }
    [JsonPropertyName("won")] public bool Won { get; set; }
}

sealed class W
{
    [JsonPropertyName("s")] public string Source { get; set; } = "";
    [JsonPropertyName("v")] public int Value { get; set; }
}

sealed class Busy
{
    [JsonPropertyName("a")] public string Action { get; set; } = "";
    [JsonPropertyName("tg")] public string Target { get; set; } = "";
    [JsonPropertyName("elapsed")] public int Elapsed { get; set; }
    [JsonPropertyName("dur")] public int Duration { get; set; }
    [JsonPropertyName("progress")] public double Progress { get; set; }
    [JsonPropertyName("influenced")] public bool Influenced { get; set; }
}

sealed class Com
{
    [JsonPropertyName("total")] public int Total { get; set; }
    [JsonPropertyName("held")] public bool Held { get; set; }
    [JsonPropertyName("terms")] public List<W> Terms { get; set; } = [];
}

sealed class Inf
{
    [JsonPropertyName("label")] public string Label { get; set; } = "";
    [JsonPropertyName("kw")] public List<string> Keywords { get; set; } = [];
    [JsonPropertyName("tg")] public string? Target { get; set; }
    [JsonPropertyName("strength")] public int Strength { get; set; }
}
