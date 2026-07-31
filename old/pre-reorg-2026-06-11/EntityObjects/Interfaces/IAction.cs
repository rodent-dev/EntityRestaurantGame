namespace EntityObjects.Interfaces;

public interface IAction
{
    string ActionName { get; }

    /// <summary>
    /// Semantic tags that classify what this action *is*.  Used by behaviors
    /// and status calculations to match actions generically instead of by name.
    /// </summary>
    HashSet<string> Keywords { get; }

    /// <summary>
    /// When true, Execute throws if item is null.
    /// When false, Execute may run without any item (item is used as an optional enhancer).
    /// </summary>
    bool RequiresItem { get; }

    /// <summary>
    /// Flat weight every candidate of this action starts with, before
    /// behaviors/status/influence are applied. Zero for most actions.
    /// </summary>
    int BaseWeight => 0;

    List<IItem> GetViableItems(Entity user, Entity target);
    void Execute(Entity user, Entity target, IItem? item);
    bool CanActorUse(Entity user);
    bool CanTarget(Entity user, Entity target);
}
