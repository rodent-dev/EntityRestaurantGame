// Decision probe: taps EntityController.DecisionObserver and reports WHERE THE
// PROBABILITY WENT, not just what happened. The soak harness counts outcomes;
// this counts temptation — the candidates that lost are the diagnosis.
//
//   dotnet run --project tools\DecisionProbe [worldSeed] [ticks] [simSeed]
//   dotnet run --project tools\DecisionProbe -- 42 1500 7 --selftest
//        run the same seeds TWICE IN ONE PROCESS and diff the beat traces.
//        Agreement here + disagreement across processes = per-process string
//        hashing is leaking into iteration order somewhere.
//   dotnet run --project tools\DecisionProbe -- 42 1500 7 --trace out.txt
//        dump the beat trace for cross-process diffing.
using EntityARestaurantGame.World;
using Game.Contracts;

int worldSeed = args.Length > 0 && int.TryParse(args[0], out int w) ? w : 42;
int ticks = args.Length > 1 && int.TryParse(args[1], out int t) ? t : 4000;
int simSeed = args.Length > 2 && int.TryParse(args[2], out int s) ? s : 1;
bool selftest = args.Contains("--selftest");
string? tracePath = args.Contains("--trace")
    ? args[Array.IndexOf(args, "--trace") + 1] : null;

if (selftest)
{
    var (_, traceA) = Run(worldSeed, ticks, simSeed, trace: true);
    var (_, traceB) = Run(worldSeed, ticks, simSeed, trace: true);
    Console.WriteLine($"in-process determinism: {(traceA.SequenceEqual(traceB) ? "PASS" : "FAIL")}"
                    + $"  ({traceA.Count} vs {traceB.Count} beats)");
    int limit = Math.Min(traceA.Count, traceB.Count);
    for (int i = 0; i < limit; i++)
        if (traceA[i] != traceB[i])
        {
            Console.WriteLine($"first divergence at beat {i}:");
            Console.WriteLine($"  A: {traceA[i]}");
            Console.WriteLine($"  B: {traceB[i]}");
            for (int k = Math.Max(0, i - 3); k < i; k++)
                Console.WriteLine($"  (same @{k}) {traceA[k]}");
            break;
        }
    return;
}

var (report, traceLines) = Run(worldSeed, ticks, simSeed, trace: tracePath is not null);
if (tracePath is not null)
{
    File.WriteAllLines(tracePath, traceLines);
    Console.WriteLine($"wrote {traceLines.Count} beats to {tracePath}");
}
Console.WriteLine(report);

// ── The run ─────────────────────────────────────────────────────────────────

static (string Report, List<string> Trace) Run(int worldSeed, int ticks, int simSeed, bool trace)
{
    var executed = new Dictionary<string, int>();
    var deferred = new Dictionary<string, int>();
    var massByAction = new Dictionary<string, double>();
    var candidatesByAction = new Dictionary<string, long>();
    var floorMassByAction = new Dictionary<string, double>();
    var sourceTotal = new Dictionary<ActionWeightSource, long>();
    var sourceCount = new Dictionary<ActionWeightSource, long>();
    var wonWhileStarving = new Dictionary<string, int>();
    var violence = new Dictionary<string, int>();
    var traceLines = new List<string>();
    double totalMass = 0, totalFloorMass = 0;
    long beats = 0, starvingBeats = 0, candidateTotal = 0;

    static void Bump<T>(Dictionary<T, int> d, T k) where T : notnull =>
        d[k] = d.TryGetValue(k, out int v) ? v + 1 : 1;
    static void Add<T>(Dictionary<T, double> d, T k, double v) where T : notnull =>
        d[k] = d.TryGetValue(k, out double c) ? c + v : v;
    static void AddL<T>(Dictionary<T, long> d, T k, long v) where T : notnull =>
        d[k] = d.TryGetValue(k, out long c) ? c + v : v;

    var world = WorldBuilder.BuildGenerated(worldSeed, simSeed);
    var controller = world.Controller;
    int startingPopulation = controller.ActiveEntities.Count;

    controller.DecisionObserver = tr =>
    {
        beats++;
        string chosen = tr.Selected.Action.ActionName;
        Bump(tr.Deferred ? deferred : executed, chosen);

        if (trace)
        {
            string shape = string.Join(",", tr.Candidates
                .GroupBy(c => c.Action.ActionName)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => $"{g.Key}:{g.Count()}"));
            string inv = string.Join("/", tr.Actor.Inventory
                .Select(i => i.ItemId).OrderBy(i => i, StringComparer.Ordinal));
            traceLines.Add($"t{tr.Tick} {tr.Actor.ID} {chosen}->{tr.Selected.Target.ID} "
                         + $"w{tr.Selected.TotalWeight} n{tr.Candidates.Count} "
                         + $"hp{tr.Actor.GetCurrentHealth():0.0} hu{tr.Actor.Needs.Hunger} "
                         + $"[{shape}] inv[{inv}]");
        }

        if (tr.Actor.Needs.HungerRatio < 0.50) { starvingBeats++; Bump(wonWhileStarving, chosen); }

        // SelectWeighted floors every candidate at 1, so a candidate the pipeline
        // actively rejected still buys a lottery ticket. Split the mass to see how
        // much of the roll is genuine appetite and how much is that floor.
        foreach (var c in tr.Candidates)
        {
            string name = c.Action.ActionName;
            int raw = c.TotalWeight;
            double mass = Math.Max(1, raw);

            candidateTotal++;
            AddL(candidatesByAction, name, 1);
            Add(massByAction, name, mass);
            totalMass += mass;
            if (raw <= 0) { Add(floorMassByAction, name, 1); totalFloorMass += 1; }

            foreach (var weight in c.Weights)
            {
                AddL(sourceTotal, weight.Source, weight.Weight);
                AddL(sourceCount, weight.Source, 1);
            }
        }

        if (!tr.Deferred && tr.Selected.Action.Keywords.Contains("Violent"))
            Bump(violence, $"{tr.Actor.EntityType} -> {tr.Selected.Target.EntityType}");
    };

    var population = new List<(int Tick, int Alive)>();
    while (controller.CurrentTick < ticks)
    {
        int before = controller.CurrentTick;
        controller.Pump(2.5);
        if (before / 500 != controller.CurrentTick / 500)
            population.Add((controller.CurrentTick, controller.ActiveEntities.Count));
    }

    // ── Report ──────────────────────────────────────────────────────────────
    var o = new System.Text.StringBuilder();
    void Rule(string title) => o.AppendLine($"\n── {title} " + new string('─', Math.Max(0, 62 - title.Length)));

    o.AppendLine($"world seed {worldSeed} · sim seed {simSeed} · {ticks} ticks · "
               + $"{startingPopulation} residents on '{world.Map!.ActiveStage.Name}'");

    Rule("POPULATION");
    o.AppendLine("  " + string.Join("  ", population.Select(p => $"t{p.Tick / 1000.0:0.0}k:{p.Alive}")));
    o.AppendLine($"  survivors {controller.ActiveEntities.Count}/{startingPopulation}"
        + (controller.ActiveEntities.Count > 0
            ? "   [" + string.Join(" ", controller.ActiveEntities.GroupBy(e => e.EntityType)
                .OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}")) + "]"
            : ""));

    Rule("BEAT CENSUS — what entities actually spent their beats on");
    o.AppendLine($"  {"action",-12} {"executed",9} {"deferred",9} {"% of beats",11}");
    foreach (var name in executed.Keys.Union(deferred.Keys)
                 .OrderByDescending(n => executed.GetValueOrDefault(n) + deferred.GetValueOrDefault(n)))
    {
        int ex = executed.GetValueOrDefault(name), df = deferred.GetValueOrDefault(name);
        o.AppendLine($"  {name,-12} {ex,9} {df,9} {(ex + df) * 100.0 / Math.Max(1, beats),10:0.0}%");
    }
    o.AppendLine($"  {"TOTAL",-12} {executed.Values.Sum(),9} {deferred.Values.Sum(),9}"
               + $"    ({beats} beats, {deferred.Values.Sum() * 100.0 / Math.Max(1, beats):0.0}% went nowhere)");

    Rule("SELECTION MASS — where the weighted roll's probability actually sat");
    o.AppendLine($"  {"action",-12} {"cand/beat",10} {"mass share",11} {"from floor",11} {"avg raw w",10}");
    foreach (var (name, mass) in massByAction.OrderByDescending(kv => kv.Value))
    {
        double floor = floorMassByAction.GetValueOrDefault(name);
        long cands = candidatesByAction[name];
        o.AppendLine($"  {name,-12} {cands / (double)Math.Max(1, beats),10:0.0} "
                   + $"{mass * 100 / totalMass,10:0.0}% {floor * 100 / Math.Max(1, mass),10:0.0}% "
                   + $"{mass / cands,10:0.0}");
    }

    Rule("THE FLOOR — SelectWeighted's Math.Max(1, w)");
    o.AppendLine($"  {totalFloorMass * 100 / totalMass:0.0}% of ALL selection probability came from candidates");
    o.AppendLine($"  the pipeline scored at zero or below ({totalFloorMass:n0} of {totalMass:n0} mass).");
    o.AppendLine($"  Candidates offered per beat: {candidateTotal / (double)Math.Max(1, beats):0.0}");

    Rule("WEIGHT SOURCES — who is actually steering");
    o.AppendLine($"  {"source",-16} {"total",12} {"applied",10} {"mean",8}");
    foreach (var (src, total) in sourceTotal.OrderByDescending(kv => Math.Abs(kv.Value)))
        o.AppendLine($"  {src,-16} {total,12:n0} {sourceCount[src],10:n0} {total / (double)sourceCount[src],8:0.0}");

    Rule("HUNGER DEFICIT BAND — what wins when hunger < 50%");
    if (starvingBeats == 0) o.AppendLine("  (nobody spent a beat below half hunger)");
    else
    {
        o.AppendLine($"  {starvingBeats:n0} beats ({starvingBeats * 100.0 / beats:0.0}% of all)");
        foreach (var (name, n) in wonWhileStarving.OrderByDescending(kv => kv.Value).Take(6))
            o.AppendLine($"  {name,-12} {n,7} {n * 100.0 / starvingBeats,8:0.0}%");
    }

    Rule("PREDATION — violent beats that landed");
    if (violence.Count == 0) o.AppendLine("  (none)");
    else
        foreach (var (pair, n) in violence.OrderByDescending(kv => kv.Value))
            o.AppendLine($"  {pair,-28} {n,6}");

    return (o.ToString(), traceLines);
}
