using Game.Contracts;

namespace Game.Services.Actions;

/// <summary>
/// Asking someone to run with you. Attached to everyone, not granted to the
/// player, because bands have to be able to form out in the world with nobody
/// watching — the player's party is just the one the camera happens to follow.
///
/// Eligibility does the gating rather than weight: PartyService decides who is
/// plausibly winnable, and only those become candidates at all. That keeps the
/// option list honest (you are never offered a recruit that could not happen)
/// and lets ordinary Social behaviour weighting decide whether an entity
/// bothers asking this beat.
///
/// A refusal is never a wall — it costs the asking, not the relationship, and
/// the same prospect can be asked again as regard grows.
/// </summary>
public sealed class RecruitAction : IAction
{
    private readonly PartyService _parties;

    public RecruitAction(PartyService parties) => _parties = parties;

    public string ActionName => "Recruit";
    public HashSet<string> Keywords { get; } = ["Social", "Diplomatic"];

    public bool RequiresItem => false;

    /// <summary>Anyone can ask — a loner asking is how a band gets founded.</summary>
    public bool CanActorUse(Entity user) => true;

    public bool CanTarget(Entity user, Entity target) =>
        _parties.IsEligible(target, user);

    public List<IItem> GetViableItems(Entity user, Entity target) => [];

    /// <summary>
    /// Put the question. Winning binds them into the band and warms both
    /// sides; losing leaves them where they were, a little flattered to have
    /// been asked. Either way the asking itself was a social act.
    /// </summary>
    public void Execute(Entity user, Entity target, IItem? item)
    {
        if (_parties.TryRecruit(target, user))
        {
            target.EntityRelationships[user.ID] =
                target.EntityRelationships.GetValueOrDefault(user.ID) + 2;
            user.EntityRelationships[target.ID] =
                user.EntityRelationships.GetValueOrDefault(target.ID) + 2;
            target.Needs.AdjustMood(5);
        }
        else
        {
            target.Needs.AdjustMood(1);
        }
    }
}
