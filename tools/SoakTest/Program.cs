// Headless soak: generate a world, run it for thousands of ticks, and report
// whether the sim holds together — survival, economy flow, violence, work.
// This is also the stress-test rig: crank archetype perSettlement ranges and
// watch throughput.
//
// Run with:  dotnet run --project tools\SoakTest [seed] [ticks]
using System.Diagnostics;
using EntityARestaurantGame.World;

int seed = args.Length > 0 && int.TryParse(args[0], out int s) ? s : 42;
int targetTicks = args.Length > 1 && int.TryParse(args[1], out int t) ? t : 4000;
bool verbose = args.Contains("-v"); // print every signal as it happens

var world = WorldBuilder.BuildGenerated(seed);
var controller = world.Controller;
Console.WriteLine(world.Map!.Summary());
Console.WriteLine($"[soak] running {targetTicks} ticks on stage '{world.Map.ActiveStage.Name}'...");
Console.WriteLine();

string[] foodIds = ["bread_loaf", "steak", "raw_meat", "coffee_cup"];
long seenSignals = 0;
int workBeats = 0, violence = 0, loots = 0, talks = 0, deaths = 0, starvePulses = 0;
var stopwatch = Stopwatch.StartNew();

void HarvestSignals()
{
    long total = controller.TotalSignals;
    int fresh = (int)Math.Min(total - seenSignals, controller.Signals.Count);
    seenSignals = total;
    for (int i = controller.Signals.Count - fresh; i < controller.Signals.Count; i++)
    {
        string m = controller.Signals[i].Message;
        if (verbose) Console.WriteLine($"t{controller.Signals[i].Tick,5}  {m}");
        if (m.Contains(": Work")) workBeats++;
        else if (m.Contains(": Attack") || m.Contains(": Grapple")) violence++;
        else if (m.Contains(" loots ")) loots++;
        else if (m.Contains(" → ")) talks++;
        else if (m.Contains("has died")) deaths++;
        else if (m.Contains("starving")) starvePulses++;
    }
}

void Snapshot()
{
    var alive = controller.ActiveEntities;
    int food = alive.Sum(e => e.Inventory.Count(i => foodIds.Contains(i.ItemId) && i.UsesRemaining != 0));
    int coins = alive.Sum(e => e.Inventory.Count(i => i.ItemId == "coin"));
    double hunger = alive.Count > 0 ? alive.Average(e => e.Needs.Hunger) : 0;
    string byType = string.Join(" ", alive.GroupBy(e => e.EntityType)
        .OrderBy(g => g.Key)
        .Select(g => $"{g.Key}:{g.Count()}"));
    Console.WriteLine(
        $"t{controller.CurrentTick,5}  alive {alive.Count,2}  food {food,3}  coins {coins,2}  hunger {hunger,3:0}  " +
        $"work {workBeats,4}  fight {violence,3}  loot {loots,2}  talk {talks,4}  dead {deaths,2}   [{byType}]");
}

// -tame: channel probe — one language-user soothes one beast repeatedly and
// we watch the relationship walk. Proves the nonverbal tone path end-to-end.
if (args.Contains("-tame"))
{
    // The natural tamer: most sociable language-user (a bandit handler just
    // tries to shake the beast down — verified emergently). Beast: gentlest
    // first, so the channel is measured before predator dynamics.
    var handler = controller.ActiveEntities.Where(e => e.Actions.HasAction("Converse"))
        .OrderByDescending(e => e.Mind.Sociability).FirstOrDefault();
    var beast = controller.ActiveEntities.Where(e => !e.Actions.HasAction("Converse"))
        .OrderByDescending(e => e.Mind.Sociability).FirstOrDefault();
    if (handler is null || beast is null)
    {
        Console.WriteLine("[tame] need one verbal and one nonverbal entity on the stage");
        return;
    }
    // Diagnostic isolation: a hostile handler growls threats between the
    // pleasantries (correct behavior — taming requires YOUR stance too), so
    // neutralize the handler's side to measure the soothing channel cleanly.
    handler.EntityRelationships[beast.ID] = 0;
    Console.WriteLine($"[tame] {handler.DisplayName} (wisdom {handler.Stats.Wisdom}) works on the {beast.DisplayName}...");
    for (int i = 1; i <= 30; i++)
    {
        world.Dialog.Converse(handler, beast);
        controller.FlushDialog();
        if (i % 10 == 0)
            Console.WriteLine($"[tame] after {i} attempts: {beast.DisplayName} → {handler.DisplayName} rel = " +
                $"{beast.EntityRelationships.GetValueOrDefault(handler.ID)}, mood {beast.Needs.Mood}");
    }
    Console.WriteLine("-- probe signals --");
    foreach (var sig in controller.Signals.TakeLast(8))
        Console.WriteLine($"t{sig.Tick,5}  {sig.Message}");
    return;
}

Snapshot();
while (controller.CurrentTick < targetTicks)
{
    int before = controller.CurrentTick;
    controller.Pump(2.5); // 5 ticks per slice at 2 t/s — signal cap outruns nothing
    HarvestSignals();
    if (before / 500 != controller.CurrentTick / 500) Snapshot();
}

stopwatch.Stop();
Console.WriteLine();
Console.WriteLine($"[soak] {targetTicks} ticks in {stopwatch.Elapsed.TotalSeconds:0.0}s " +
    $"({targetTicks / Math.Max(0.001, stopwatch.Elapsed.TotalSeconds):0} ticks/s)");
Console.WriteLine($"[soak] survivors {controller.ActiveEntities.Count}/{controller.ActiveEntities.Count + controller.InactiveEntities.Count}  " +
    $"work {workBeats}  violence {violence}  loots {loots}  talks {talks}  deaths {deaths}  starvation-pulses {starvePulses}");
Console.WriteLine();
Console.WriteLine("-- last 15 signals --");
foreach (var sig in controller.Signals.TakeLast(15))
    Console.WriteLine($"t{sig.Tick,5}  {sig.Message}");
