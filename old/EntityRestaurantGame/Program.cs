using EntityObjects;
using EntityObjects.Interfaces;

Console.WriteLine("=== Entity Behavior Testing System ===\n");

// Initialize two entities for a simple combat scenario
var attacker = new Entity
{
    ID = "attacker",
    DisplayName = "Aggressive Warrior",
    EntityType = "Warrior",
    Inventory = new List<IItem>() { new Fist() }
};

var defender = new Entity
{
    ID = "defender",
    DisplayName = "Cautious Guard",
    EntityType = "Guard"
};

var merchant = new Entity
{
    ID = "merchant",
    DisplayName = "Friendly Merchant",
    EntityType = "Merchant",
    Inventory = new List<IItem>() { new Gift() }
};

// Assign actions
attacker.GetActionModule().AttachAction(new AttackAction());
defender.GetActionModule().AttachAction(new DefendAction());
merchant.GetActionModule().AttachAction(new SpeakAction());

Console.WriteLine("--- Initial State ---");
Console.WriteLine($"{attacker.DisplayName} health: {attacker.GetCurrentHealth()}");
Console.WriteLine($"{defender.DisplayName} health: {defender.GetCurrentHealth()}");
Console.WriteLine($"{merchant.DisplayName} inventory: {merchant.Inventory.Count} items");
Console.WriteLine($"{defender.DisplayName} inventory: {defender.Inventory.Count} items");
Console.WriteLine();

// Simple combat loop
for (int i = 0; i < 3; i++)
{
    Console.WriteLine($"--- Turn {i + 1} ---");

    // Attacker's turn
    var attackAction = attacker.GetActionModule().GetAction("Attack");
    var fist = attacker.Inventory.First(item => item is Fist);
    if (attackAction != null && fist != null)
    {
        Console.WriteLine($"{attacker.DisplayName} attacks {defender.DisplayName} with their fist!");
        attackAction.Execute(attacker, defender, fist); 
        Console.WriteLine($"{defender.DisplayName} has {defender.GetCurrentHealth()} health remaining.");
    }

    // Defender's turn
    var defendAction = defender.GetActionModule().GetAction("Defend");
    if (defendAction != null)
    {
        Console.WriteLine($"{defender.DisplayName} defends!");
        defendAction.Execute(defender, defender, new Fist()); // Dummy item for defend
    }

    // Merchant's turn
    var speakAction = merchant.GetActionModule().GetAction("Speak");
    var gift = merchant.Inventory.FirstOrDefault(item => item is Gift);
    if (speakAction != null && gift != null)
    {
        Console.WriteLine($"{merchant.DisplayName} gives a gift to {defender.DisplayName}.");
        speakAction.Execute(merchant, defender, gift);
        Console.WriteLine($"{defender.DisplayName}'s inventory now has {defender.Inventory.Count} items.");
        Console.WriteLine($"{merchant.DisplayName}'s inventory now has {merchant.Inventory.Count} items.");
    }

    Console.WriteLine();
}

Console.WriteLine("\n=== Test Complete ===");
