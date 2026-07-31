using System.Numerics;
using EntityObjects;
using EntityObjects.Actions;
using EntityObjects.Effects;
using EntityObjects.Interfaces;
using EntityObjects.Items;
using EntityObjects.Spatial;
using EntityARestaurantGame;
using EntityARestaurantGame.UI;
using EntityARestaurantGame.UI.Screens;

// ── Effect registry ───────────────────────────────────────────────────────
var effects = new EffectRegistry();
effects.Register("Damage",       new DamageEffect());
effects.Register("Heal",         new HealEffect());
effects.Register("Feed",         new FeedEffect());
effects.Register("Relationship", new RelationshipEffect());
effects.Register("ConsumeItem",  new ConsumeItemEffect());

// ── Item factory ──────────────────────────────────────────────────────────
var factory  = new ItemFactory(effects);
string itemsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "items");
IItem Item(string id) => factory.LoadFromFile(Path.Combine(itemsDir, $"{id}.json"));

// ── Location registry ─────────────────────────────────────────────────────
var locations = new LocationRegistry();
locations.Register(new Location("restaurant",   new Vector3(0,  0,  0), new Vector3(20, 20, 0.99f)));
locations.Register(new Location("kitchen",      new Vector3(0,  0,  0), new Vector3(10,  8, 0.99f)));
locations.Register(new Location("dining_floor", new Vector3(0,  8,  0), new Vector3(20, 20, 0.99f)));

// ── Entities ──────────────────────────────────────────────────────────────
var warrior = new Entity
{
    ID = "warrior", DisplayName = "Aldric", EntityType = "Warrior",
    Inventory = [Item("fist"), Item("kitchen_knife"), Item("bread_loaf"), Item("hex_token")],
    Position = new Vector3(5, 3, 0)
};
warrior.Behaviors.Add(new BehaviorModule("Wrath",      ["Aggressive", "Violent"],          affect: 5));
warrior.Behaviors.Add(new BehaviorModule("Cunning",    ["Aggressive", "Reactive"],         affect: 3));
warrior.Behaviors.Add(new BehaviorModule("Hunger",     ["Greedy"],                         affect: 1));
warrior.GetActionModule().AttachAction(new AttackAction());
warrior.GetActionModule().AttachAction(new DefendAction());
warrior.GetActionModule().AttachAction(new GrappleAction());
warrior.GetActionModule().AttachAction(new ConsumeAction());
warrior.GetActionModule().AttachAction(new EnchantAction());
warrior.GetActionModule().AttachAction(new IdleAction());

var guard = new Entity
{
    ID = "guard", DisplayName = "Sera", EntityType = "Guard",
    Inventory = [Item("fist"), Item("rolling_pin"), Item("coffee_cup"), Item("blessing_herb")],
    Position = new Vector3(12, 14, 0)
};
guard.Behaviors.Add(new BehaviorModule("Vigilance",  ["Defensive", "Reactive"],           affect: 4));
guard.Behaviors.Add(new BehaviorModule("Aggression", ["Aggressive", "Violent"],           affect: 2));
guard.Behaviors.Add(new BehaviorModule("Protective", ["Supportive", "Social"],            affect: 3));
guard.Behaviors.Add(new BehaviorModule("Resilience", ["SelfPreserving", "Greedy"],        affect: 2));
guard.GetActionModule().AttachAction(new AttackAction());
guard.GetActionModule().AttachAction(new DefendAction());
guard.GetActionModule().AttachAction(new GrappleAction());
guard.GetActionModule().AttachAction(new ConsumeAction());
guard.GetActionModule().AttachAction(new EnchantAction());
guard.GetActionModule().AttachAction(new IdleAction());

var merchant = new Entity
{
    ID = "merchant", DisplayName = "Finn", EntityType = "Merchant",
    Inventory = [Item("bouquet"), Item("wine_bottle"), Item("business_card"), Item("steak"), Item("blessing_herb")],
    Position = new Vector3(9, 12, 0)
};
merchant.Behaviors.Add(new BehaviorModule("Charm",    ["Social", "Diplomatic"],            affect: 6));
merchant.Behaviors.Add(new BehaviorModule("Goodwill", ["Supportive", "Social"],           affect: 4));
merchant.Behaviors.Add(new BehaviorModule("Caution",  ["SelfPreserving"],                 affect: 2));
merchant.GetActionModule().AttachAction(new SpeakAction());
merchant.GetActionModule().AttachAction(new EnchantAction());
merchant.GetActionModule().AttachAction(new ConsumeAction());
merchant.GetActionModule().AttachAction(new IdleAction());
merchant.EntityRelationships["guard"] = 5;

// ── Controller ────────────────────────────────────────────────────────────
var controller = new EntityController(locations);
controller.AddEntity(warrior);
controller.AddEntity(guard);
controller.AddEntity(merchant);

// ── Start-anew spawner: a fresh pawn, known by no one ─────────────────────
int drifterCount = 0;
Entity SpawnNobody()
{
    drifterCount++;
    var pawn = new Entity
    {
        ID = $"drifter_{drifterCount}",
        DisplayName = $"Drifter {drifterCount}",
        EntityType = "Nobody",
        Inventory = [Item("fist"), Item("bread_loaf")],
        Position = new Vector3(10, 18, 0)   // just inside the door
    };
    pawn.Behaviors.Add(new BehaviorModule("Wary", ["SelfPreserving", "Reactive"], affect: 3));
    pawn.GetActionModule().AttachAction(new AttackAction());
    pawn.GetActionModule().AttachAction(new DefendAction());
    pawn.GetActionModule().AttachAction(new ConsumeAction());
    pawn.GetActionModule().AttachAction(new IdleAction());
    return pawn;
}

// ── UI ────────────────────────────────────────────────────────────────────
var renderer = new ConsoleRenderer();
var manager  = new ScreenManager(renderer);
manager.Push(new PawnSelectScreen(controller, SpawnNobody));
manager.Run();
