using Game.Contracts;

namespace Game.Services;

/// <summary>One verb as the player sees it: can you ask for this, and if not, why not.</summary>
public sealed class Affordance
{
    public required IAction Action { get; init; }
    public bool Available { get; init; }

    /// <summary>Populated only when unavailable. Player-facing prose.</summary>
    public string? Reason { get; init; }

    /// <summary>Who this verb could be aimed at right now, nearest first.</summary>
    public IReadOnlyList<Entity> Targets { get; init; } = [];

    /// <summary>Whether the verb can be pointed at its own user.</summary>
    public bool SelfUsable { get; init; }
}

/// <summary>
/// The curated player surface: every verb a pawn owns, whether it is askable,
/// and a sentence saying why when it is not.
/// </summary>
/// <remarks>
/// A projection, never a gate. NPCs run the raw pipeline and are unaffected by
/// anything here; this only decides what the wheel shows and how it reads.
/// <para>
/// Blocked entries are kept deliberately rather than filtered out, and that is
/// the load-bearing decision. A menu that silently omits what you cannot do
/// reads as a menu that is missing things; one that shows it greyed with
/// "nothing on you to do that with" reads as a world with rules. Knowing why
/// you <em>can't</em> is most of what makes a limit feel like a place rather
/// than an unfinished feature.
/// </para>
/// <para>
/// The verb set itself is stable — <see cref="ActionModule"/> is populated at
/// world build and never changes — so the wheel's SHAPE can be built once per
/// pawn. Only the leaves (targets, items) churn beat to beat, which is why this
/// resolves them per call and leaves the ring structure to the caller.
/// </para>
/// </remarks>
public static class PlayerAffordances
{
    /// <summary>Every verb the pawn owns, askable or not.</summary>
    public static List<Affordance> For(Entity pawn, IReadOnlyList<Entity> roster)
    {
        var result = new List<Affordance>();

        foreach (var action in pawn.Actions.GetAllActions())
        {
            var targets = roster
                .Where(e => e.ID != pawn.ID && e.IsAlive && action.CanTarget(pawn, e))
                .OrderBy(e => System.Numerics.Vector3.Distance(pawn.Position, e.Position))
                .ToList();

            bool selfUsable = action.CanTarget(pawn, pawn);

            result.Add(new Affordance
            {
                Action = action,
                Available = Blocker(pawn, action, targets, selfUsable) is null,
                Reason = Blocker(pawn, action, targets, selfUsable),
                Targets = targets,
                SelfUsable = selfUsable,
            });
        }

        return result;
    }

    /// <summary>Only the rings that have something in them for this pawn.</summary>
    /// <remarks>
    /// An empty drawer is worse than a missing one: it costs a keystroke to
    /// discover it was never going to help. A verb no ring claims still gets
    /// shown, under its own heading, because a verb the player cannot reach is
    /// a content bug and hiding it is how it stays one.
    /// </remarks>
    public static List<(ActionCategory Category, List<Affordance> Verbs)> ByCategory(
        Entity pawn, IReadOnlyList<Entity> roster)
    {
        var all = For(pawn, roster);
        var rings = new List<(ActionCategory, List<Affordance>)>();

        foreach (var category in ActionTaxonomy.Categories)
        {
            var verbs = all.Where(a => category.Contains(a.Action))
                           .OrderBy(a => a.Action.ActionName, StringComparer.Ordinal)
                           .ToList();
            if (verbs.Count > 0) rings.Add((category, verbs));
        }

        var orphans = all.Where(a => ActionTaxonomy.IsUncategorised(a.Action))
                         .OrderBy(a => a.Action.ActionName, StringComparer.Ordinal)
                         .ToList();
        if (orphans.Count > 0)
        {
            rings.Add((new ActionCategory("Other", [], "verbs no ring claims yet"), orphans));
        }

        return rings;
    }

    /// <summary>
    /// Why this verb is not askable, or null if it is.
    /// </summary>
    /// <remarks>
    /// Ordered by which obstacle the player can do something about. "Nobody to
    /// do it to" is a fact about the room and reads as a nudge to go somewhere;
    /// "nothing on you" is a fact about your pack and reads as a nudge to go
    /// and get something. Reporting whichever is checked first would sometimes
    /// name the one you cannot fix.
    /// </remarks>
    private static string? Blocker(
        Entity pawn, IAction action, IReadOnlyList<Entity> targets, bool selfUsable)
    {
        if (targets.Count == 0 && !selfUsable)
            return "there's nobody for that";

        if (action.RequiresItem && action.CanActorUse(pawn) is false)
            return "nothing on you to do that with";

        if (!action.CanActorUse(pawn))
            return "not right now";

        // A verb that needs an item and has one in principle can still have
        // nothing viable against the people actually here.
        if (action.RequiresItem && targets.Count > 0 &&
            targets.All(t => action.GetViableItems(pawn, t).Count == 0) &&
            (!selfUsable || action.GetViableItems(pawn, pawn).Count == 0))
        {
            return "nothing on you works on them";
        }

        return null;
    }

    /// <summary>
    /// What the pawn could bring to bear on this target — deduplicated by item
    /// id, because "Fist, Fist, Kitchen Knife" is a worse menu than "Fist,
    /// Kitchen Knife" and the sim does not care which copy it gets.
    /// </summary>
    public static List<IItem> ItemChoices(Entity pawn, IAction action, Entity target)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var choices = new List<IItem>();
        foreach (var item in action.GetViableItems(pawn, target))
            if (seen.Add(item.ItemId)) choices.Add(item);
        return choices;
    }
}
