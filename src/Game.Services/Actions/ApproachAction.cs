using System.Numerics;
using Game.Contracts;

namespace Game.Services.Actions;

/// <summary>
/// Walking, as a first-class candidate. Movement used to be a hardcoded gate
/// in the decision loop: reachable targets got acted on, far ones silently
/// turned the beat into a step. That worked for the sim but left the player
/// mute — you cannot whisper "go to him" if going is not a thing anyone can
/// choose. As an action it competes, weighs, and takes influence like any
/// other verb.
///
/// It carries no BaseWeight on purpose. Nothing endorses walking for its own
/// sake, so it never wins a beat spontaneously (Idle's BaseWeight alone
/// outbids it) — it wins when something WANTS it: a whisper, or the reach
/// gate that still substitutes it when a chosen target is too far away.
/// </summary>
public sealed class ApproachAction : IAction
{
    public string ActionName => "Approach";
    public HashSet<string> Keywords { get; } = [EntityService.MovementKeyword];

    public bool RequiresItem => false;

    public bool CanActorUse(Entity user) => true;
    public bool CanTarget(Entity user, Entity target) => user.ID != target.ID;

    public List<IItem> GetViableItems(Entity user, Entity target) => [];

    /// <summary>
    /// One beat of travel, sized by the mover's own speed, stopping at reach
    /// rather than at the target — arriving means "close enough to act", not
    /// standing inside them. No world clamp needed: a step toward something
    /// already inside the world never leaves it.
    /// </summary>
    public void Execute(Entity user, Entity target, IItem? item)
    {
        var delta = target.Position - user.Position;
        float distance = delta.Length();
        float stopAt = EntityService.ReachDistance(user, target);
        if (distance < 0.01f || distance <= stopAt) return;

        float step = Math.Min(EntityService.MoveSpeed(user), distance - stopAt);
        user.Position += Vector3.Normalize(delta) * step;
    }
}
