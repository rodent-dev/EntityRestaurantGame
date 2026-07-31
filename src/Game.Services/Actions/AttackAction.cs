using Game.Contracts;

namespace Game.Services.Actions;

public class AttackAction : IAction
{
    public string ActionName => "Attack";
    public HashSet<string> Keywords { get; } = ["Aggressive", "Violent", "Offensive"];

    public bool RequiresItem => true;

    public bool CanActorUse(Entity user) => user.Inventory.Any(i => i.CanEnableAction("Attack", user, user));
    public bool CanTarget(Entity user, Entity target) => user.ID != target.ID;

    public List<IItem> GetViableItems(Entity user, Entity target) =>
        user.Inventory.Where(i => i.CanEnableAction("Attack", user, target)).ToList();

    public void Execute(Entity user, Entity target, IItem? item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Apply("Attack", user, target);
    }
}
