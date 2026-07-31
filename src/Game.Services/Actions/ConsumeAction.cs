using Game.Contracts;

namespace Game.Services.Actions;

/// <summary>
/// Self-targeted action. Uses food, drink, or other consumable items to restore health or stats.
/// Pairs with Cautious behavior — self-preservation.
/// </summary>
public class ConsumeAction : IAction
{
    public string ActionName => "Consume";
    public HashSet<string> Keywords { get; } = ["SelfPreserving", "Greedy"];

    public bool RequiresItem => true;

    public bool CanActorUse(Entity user) =>
        user.Inventory.Any(i => i.CanEnableAction("Consume", user, user));

    public bool CanTarget(Entity user, Entity target) => user.ID == target.ID;

    public List<IItem> GetViableItems(Entity user, Entity target) =>
        user.Inventory.Where(i => i.CanEnableAction("Consume", user, target)).ToList();

    public void Execute(Entity user, Entity target, IItem? item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Apply("Consume", user, target);
    }
}
