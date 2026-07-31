using EntityObjects.Interfaces;

namespace EntityObjects.Actions;

/// <summary>
/// Targets any entity — ally or enemy. Applies a modifier via an enchantment item.
/// Positive enchantments (blessings) help; negative (curses) hinder.
/// Pairs with Social, Protective, and Curious behavior types.
/// </summary>
public class EnchantAction : IAction
{
    public string ActionName => "Enchant";
    public HashSet<string> Keywords { get; } = ["Social", "Supportive"];

    public bool RequiresItem => true;

    public bool CanActorUse(Entity user) =>
        user.Inventory.Any(i => i.CanEnableAction("Enchant", user, user));

    public bool CanTarget(Entity user, Entity target) => user.ID != target.ID;

    public List<IItem> GetViableItems(Entity user, Entity target) =>
        user.Inventory.Where(i => i.CanEnableAction("Enchant", user, target)).ToList();

    public void Execute(Entity user, Entity target, IItem? item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Apply("Enchant", user, target);
    }
}
