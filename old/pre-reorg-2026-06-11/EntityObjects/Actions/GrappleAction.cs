using EntityObjects.Interfaces;

namespace EntityObjects.Actions;

/// <summary>
/// Physical control action — disrupts a target without purely focusing on damage.
/// Deals minor damage and degrades the target's relationship toward the user.
/// Pairs with Protective and Cautious behavior types.
/// </summary>
public class GrappleAction : IAction
{
    public string ActionName => "Grapple";
    public HashSet<string> Keywords { get; } = ["Aggressive", "Reactive", "Defensive"];

    public bool RequiresItem => true;

    public bool CanActorUse(Entity user) =>
        user.Inventory.Any(i => i.CanEnableAction("Grapple", user, user));

    public bool CanTarget(Entity user, Entity target) => user.ID != target.ID;

    public List<IItem> GetViableItems(Entity user, Entity target) =>
        user.Inventory.Where(i => i.CanEnableAction("Grapple", user, target)).ToList();

    public void Execute(Entity user, Entity target, IItem? item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Apply("Grapple", user, target);
    }
}
