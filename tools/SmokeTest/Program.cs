// Headless smoke test: drives the tick-based controller without the console UI.
// Run with:  dotnet run --project tools\SmokeTest
// World setup comes from WorldBuilder (same as the game — no drift); this file
// only owns the exercise script.
using EntityARestaurantGame.World;
using Game.Contracts;
using Game.Services;

var world = WorldBuilder.BuildDefault();
var controller = world.Controller;
var dialog = world.Dialog;

var warrior  = controller.ActiveEntities.First(e => e.ID == "warrior");
var guard    = controller.ActiveEntities.First(e => e.ID == "guard");
var merchant = controller.ActiveEntities.First(e => e.ID == "merchant");

controller.FavoredEntityId = "warrior";

// Nudge the warrior toward eating, like the player would.
warrior.PendingInfluence = new Influence
{
    Keywords = ["SelfPreserving", "Greedy"],   // what Consume is made of
    Label = "Consume",
    Strength = 12,
};

// 1. Beat-stepping: 60 manual beats — enough for talk to surface desires
//    and for desire to flip the selector into the exchange path.
for (int i = 0; i < 60; i++) controller.AdvanceToNextBeat();
Console.WriteLine($"-- after 60 beats, tick={controller.CurrentTick} --");
foreach (var s in controller.Signals)
    Console.WriteLine($"t{s.Tick,5}  {s.Message}");
Console.WriteLine();

// 2. Dialog pipeline: whisper a rumor through the digestion gates while
//    everyone is still alive, then dump minds to show belief drift/desires.
Console.WriteLine("-- whisper: smear the merchant's commerce to the guard --");
dialog.Whisper(guard, new Claim(merchant.ID, merchant.DisplayName, Axes.Commerce, -0.5), throughPawn: null);
controller.FlushDialog();
foreach (var s in controller.Signals.TakeLast(3))
    Console.WriteLine($"t{s.Tick,5}  {s.Message}");

Console.WriteLine();
Console.WriteLine("-- minds --");
foreach (var e in controller.ActiveEntities)
{
    Console.WriteLine($"{e.DisplayName} (skept {e.Mind.Skepticism:0.0}, impuls {e.Mind.Impulsiveness:0.0})");
    foreach (var (objectId, sheet) in e.Mind.Beliefs)
        Console.WriteLine($"  believes {objectId,-14} {string.Join("  ", sheet.Select(kv => $"{kv.Key} {kv.Value:+0.00;-0.00}"))}");
    foreach (var d in e.Mind.Desires)
        Console.WriteLine($"  desires  {d.ObjectName,-14} ({d.Context}, t{d.FirstSurfacedTick}){(d.PinnedWant > 0 ? $"  OBSESSED pin={d.PinnedWant:0.00}" : "")}");
    foreach (var (id, score) in e.EntityRelationships)
        Console.WriteLine($"  relation {id,-14} {score:+#;-#;0}");
}
Console.WriteLine();

// 6. The whisper wheel: what the player can actually reach. The verb set is
//    fixed at world build, so this tree is stable for a pawn's whole life —
//    only the leaves (who, what with) churn. Blocked verbs stay listed with
//    their reason, because a hidden limit reads as a missing feature.
Console.WriteLine();
Console.WriteLine("-- whisper wheel --");
foreach (var pawn in controller.ActiveEntities.Take(2))
{
    Console.WriteLine($"  {pawn.DisplayName} ({pawn.EntityType})");
    foreach (var (category, verbs) in PlayerAffordances.ByCategory(pawn, controller.ActiveEntities))
    {
        Console.WriteLine($"    {category.Name,-8} {verbs.Count(v => v.Available)}/{verbs.Count}  — {category.Blurb}");
        foreach (var verb in verbs)
        {
            var items = PlayerAffordances.ItemChoices(pawn, verb.Action, pawn);
            string leaves = verb.Available
                ? $"{verb.Targets.Count} target(s)" + (items.Count > 1 ? $", {items.Count} item(s)" : "")
                : $"BLOCKED — {verb.Reason}";
            Console.WriteLine($"      {verb.Action.ActionName,-10} [{string.Join(",", verb.Action.Keywords)}]  {leaves}");
        }
    }
}

// 3. Real-time pump: simulate 600s of wall clock in 100ms slices —
// long enough for food to run out and starvation deaths to land.
for (int i = 0; i < 6000; i++) controller.Pump(0.1);
Console.WriteLine($"-- after 600s pumped, tick={controller.CurrentTick} --");

foreach (var e in controller.ActiveEntities)
    Console.WriteLine($"ACTIVE  {e.DisplayName,-8} HP {e.GetCurrentHealth():F0}/{e.GetMaxHealth():F0}  HGR {e.Needs.Hunger}  MOOD {e.Needs.Mood}  POS ({e.Position.X:0.0},{e.Position.Y:0.0}) {controller.LocationTag(e)}");
foreach (var e in controller.InactiveEntities)
    Console.WriteLine($"DEAD    {e.DisplayName,-8} HP {e.GetCurrentHealth():F0}");

Console.WriteLine();
Console.WriteLine("-- last 25 signals --");
foreach (var s in controller.Signals.TakeLast(25))
    Console.WriteLine($"t{s.Tick,5}  {s.Message}");

// 4. The event spine: every announcement is now a typed row with an audience,
//    so the check that matters is whether anyone who was NOT involved heard
//    anything. A world where only actor and target ever hear is the thing the
//    distributor exists to end.
Console.WriteLine();
Console.WriteLine("-- world events: the typed ledger --");
var ledger = controller.Events.Ledger;
Console.WriteLine($"{ledger.Count} rows held (of {controller.Events.TotalPublished} published)");
foreach (var group in ledger.GroupBy(e => e.Kind).OrderByDescending(g => g.Count()))
    Console.WriteLine($"  {group.Key,-10} {group.Count(),4}");

var events = controller.Events;
Console.WriteLine($"  reached: {events.ReachedActors} actors, {events.ReachedParticipants} " +
    $"participants, {events.ReachedWitnesses} witnesses (radius {EventDistributor.WitnessRadius:0}m)");
Console.WriteLine($"  {ledger.Count(e => e.Loud)} of the held rows were loud enough to write down.");

var roster = controller.ActiveEntities.Concat(controller.InactiveEntities).ToList();

Console.WriteLine();
Console.WriteLine("-- dossiers: how much of each log is other people's business --");
foreach (var e in roster)
{
    var log = controller.GetLog(e.ID);
    int asActor = log.Count(l => l.AsActor);
    Console.WriteLine($"  {e.DisplayName,-8} {log.Count,3} entries  {asActor,3} as protagonist  " +
        $"{log.Count - asActor,3} as onlooker");
}

// 5. Long-running actions: a job is a commitment, and what it pays depends on
//    how much of it got worked. Anything still in flight names its own score.
Console.WriteLine();
Console.WriteLine("-- activities in flight --");
foreach (var e in controller.ActiveEntities.Where(e => e.Current is not null))
{
    var activity = e.Current!;
    var action = e.Actions.GetAction(activity.ActionName);
    int duration = action?.Duration ?? 1;
    Console.WriteLine($"  {e.DisplayName,-8} {activity.ActionName,-8} " +
        $"beat {activity.Elapsed}/{duration}  banked {activity.Progress:0.00}  " +
        $"on course for {activity.PerformanceAgainst(duration):0.00}  " +
        $"focus now {EntityService.Focus(e):0.00}");
    if (controller.GetCommitment(e.ID) is { } commitment)
        Console.WriteLine($"           {commitment.Explain()}");
}
