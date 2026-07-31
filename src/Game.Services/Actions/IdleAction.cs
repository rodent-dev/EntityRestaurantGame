using Game.Contracts;

namespace Game.Services.Actions;

/// <summary>
/// The do-nothing fallback. Always available, self-targeted, item-free.
/// Gives entities a real choice NOT to act every beat; resting recovers a
/// little mood. Its BaseWeight is what every other action has to outweigh.
/// </summary>
public class IdleAction : IAction
{
    public string ActionName => "Idle";
    public HashSet<string> Keywords { get; } = ["SelfPreserving"];

    public bool RequiresItem => false;
    public int BaseWeight => 8;

    public bool CanActorUse(Entity user) => true;
    public bool CanTarget(Entity user, Entity target) => user.ID == target.ID;

    public List<IItem> GetViableItems(Entity user, Entity target) => [];

    public void Execute(Entity user, Entity target, IItem? item) =>
        user.Needs.AdjustMood(2);
}
