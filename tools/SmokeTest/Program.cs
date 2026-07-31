// Headless smoke test: drives the tick-based controller without the console UI.
// Run with:  dotnet run --project tools\SmokeTest
// World setup comes from WorldBuilder (same as the game — no drift); this file
// only owns the exercise script.
using EntityARestaurantGame.World;
using Game.Contracts;

var world = WorldBuilder.BuildDefault();
var controller = world.Controller;
var dialog = world.Dialog;

var warrior  = controller.ActiveEntities.First(e => e.ID == "warrior");
var guard    = controller.ActiveEntities.First(e => e.ID == "guard");
var merchant = controller.ActiveEntities.First(e => e.ID == "merchant");

controller.FavoredEntityId = "warrior";

// Nudge the warrior toward eating, like the player would.
warrior.PendingInfluence = new Influence { TargetActionName = "Consume", Strength = 12 };

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
