// Headless smoke test: drives the tick-based controller without the console UI.
// Run with:  dotnet run --project tools\SmokeTest
using System.Numerics;
using EntityARestaurantGame;
using EntityObjects;
using EntityObjects.Actions;
using EntityObjects.Effects;
using EntityObjects.Interfaces;
using EntityObjects.Items;
using EntityObjects.Spatial;

// Locate the repo-root data\items folder by walking up from the output dir.
string dir = AppContext.BaseDirectory;
while (!Directory.Exists(Path.Combine(dir, "data", "items")))
    dir = Path.GetDirectoryName(dir) ?? throw new DirectoryNotFoundException("data\\items not found");
string itemsDir = Path.Combine(dir, "data", "items");

var effects = new EffectRegistry();
effects.Register("Damage",       new DamageEffect());
effects.Register("Heal",         new HealEffect());
effects.Register("Feed",         new FeedEffect());
effects.Register("Relationship", new RelationshipEffect());
effects.Register("ConsumeItem",  new ConsumeItemEffect());

var factory = new ItemFactory(effects);
IItem Item(string id) => factory.LoadFromFile(Path.Combine(itemsDir, $"{id}.json"));

var warrior = new Entity
{
    ID = "warrior", DisplayName = "Aldric", EntityType = "Warrior",
    Inventory = [Item("fist"), Item("kitchen_knife"), Item("bread_loaf"), Item("hex_token")],
    Position = new Vector3(5, 3, 0)
};
warrior.Behaviors.Add(new BehaviorModule("Wrath",   ["Aggressive", "Violent"],  affect: 5));
warrior.Behaviors.Add(new BehaviorModule("Cunning", ["Aggressive", "Reactive"], affect: 3));
warrior.Behaviors.Add(new BehaviorModule("Hunger",  ["Greedy"],                 affect: 1));
foreach (IAction a in (IAction[])[new AttackAction(), new DefendAction(), new GrappleAction(), new ConsumeAction(), new EnchantAction(), new IdleAction()])
    warrior.GetActionModule().AttachAction(a);

var guard = new Entity
{
    ID = "guard", DisplayName = "Sera", EntityType = "Guard",
    Inventory = [Item("fist"), Item("rolling_pin"), Item("coffee_cup"), Item("blessing_herb")],
    Position = new Vector3(12, 14, 0)
};
guard.Behaviors.Add(new BehaviorModule("Vigilance",  ["Defensive", "Reactive"],    affect: 4));
guard.Behaviors.Add(new BehaviorModule("Aggression", ["Aggressive", "Violent"],    affect: 2));
guard.Behaviors.Add(new BehaviorModule("Protective", ["Supportive", "Social"],     affect: 3));
guard.Behaviors.Add(new BehaviorModule("Resilience", ["SelfPreserving", "Greedy"], affect: 2));
foreach (IAction a in (IAction[])[new AttackAction(), new DefendAction(), new GrappleAction(), new ConsumeAction(), new EnchantAction(), new IdleAction()])
    guard.GetActionModule().AttachAction(a);

var merchant = new Entity
{
    ID = "merchant", DisplayName = "Finn", EntityType = "Merchant",
    Inventory = [Item("bouquet"), Item("wine_bottle"), Item("business_card"), Item("steak"), Item("blessing_herb")],
    Position = new Vector3(9, 12, 0)
};
merchant.Behaviors.Add(new BehaviorModule("Charm",    ["Social", "Diplomatic"], affect: 6));
merchant.Behaviors.Add(new BehaviorModule("Goodwill", ["Supportive", "Social"], affect: 4));
merchant.Behaviors.Add(new BehaviorModule("Caution",  ["SelfPreserving"],       affect: 2));
foreach (IAction a in (IAction[])[new SpeakAction(), new EnchantAction(), new ConsumeAction(), new IdleAction()])
    merchant.GetActionModule().AttachAction(a);

var locations = new LocationRegistry();
locations.Register(new Location("restaurant",   new Vector3(0, 0, 0), new Vector3(20, 20, 0.99f)));
locations.Register(new Location("kitchen",      new Vector3(0, 0, 0), new Vector3(10,  8, 0.99f)));
locations.Register(new Location("dining_floor", new Vector3(0, 8, 0), new Vector3(20, 20, 0.99f)));

var controller = new EntityController(locations);
controller.AddEntity(warrior);
controller.AddEntity(guard);
controller.AddEntity(merchant);
controller.FavoredEntityId = "warrior";

// Nudge the warrior toward eating, like the player would.
warrior.PendingInfluence = new Influence { TargetActionName = "Consume", Strength = 12 };

// 1. Beat-stepping: 20 manual beats.
for (int i = 0; i < 20; i++) controller.AdvanceToNextBeat();
Console.WriteLine($"-- after 20 beats, tick={controller.CurrentTick} --");
foreach (var s in controller.Signals)
    Console.WriteLine($"t{s.Tick,5}  {s.Message}");
Console.WriteLine();

// 2. Real-time pump: simulate 600s of wall clock in 100ms slices —
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
