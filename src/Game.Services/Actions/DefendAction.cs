using Game.Contracts;

namespace Game.Services.Actions;

public class DefendAction : IAction
{
    public string ActionName => "Defend";
    public HashSet<string> Keywords { get; } = ["SelfPreserving", "Reactive", "Defensive"];

    public bool RequiresItem => true;

    public bool CanActorUse(Entity user) => user.Inventory.Any(i => i.CanEnableAction("Defend", user, user));
    public bool CanTarget(Entity user, Entity target) => user.ID == target.ID;

    public List<IItem> GetViableItems(Entity user, Entity target) =>
        user.Inventory.Where(i => i.CanEnableAction("Defend", user, target)).ToList();

    public void Execute(Entity user, Entity target, IItem? item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Apply("Defend", user, target);
    }
}
