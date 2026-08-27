namespace Game.Contracts;

/// <summary>
/// One ring of the player's action wheel: a name, and the keywords that decide
/// what falls under it.
/// </summary>
/// <remarks>
/// Membership is by keyword ANY-match, so an action can legitimately belong to
/// two categories at once — <c>Defend</c> is <c>SelfPreserving, Reactive,
/// Defensive</c> and belongs under both Combat and Self. That is correct rather
/// than sloppy: "fight" and "look after yourself" should both surface it, and
/// forcing a single home would mean choosing which of two true things to hide.
/// The wheel is a graph, not a tree.
/// </remarks>
public sealed record ActionCategory(string Name, IReadOnlyList<string> Keywords, string Blurb)
{
    public bool Contains(IAction action)
    {
        foreach (var keyword in Keywords)
            if (action.Keywords.Contains(keyword)) return true;
        return false;
    }
}

/// <summary>
/// The keyword vocabulary, and the groupings built on it.
/// </summary>
/// <remarks>
/// One home for every "these keywords mean roughly this" list in the game. It
/// exists because the same taxonomy has two consumers who must never disagree:
/// <see cref="Game.Services.EntityService"/>'s relationship weighting (does this
/// verb warm someone or threaten them?) and the player's action wheel (what
/// drawer do I find it in?). Those lists were previously private fields in the
/// weighting code, which meant the interface had no way to ask the question the
/// sim was already answering.
/// <para>
/// Everything here is keyed on KEYWORDS. Nothing in this file may ever mention
/// an action name — that is the same rule the weight systems live under, and
/// the wheel is a weight system now: whispering is an additive weight source.
/// </para>
/// </remarks>
public static class ActionTaxonomy
{
    // ── Weighting groups (moved verbatim from EntityService) ─────────────

    /// <summary>Verbs that read as a threat to whoever they land on.</summary>
    /// <remarks>
    /// Deliberately excludes <c>Defensive</c>: defending yourself near someone
    /// is not an act against them, and folding it in here would make a guard
    /// standing their ground read as hostility to a friend.
    /// </remarks>
    public static readonly string[] Hostile = ["Aggressive", "Violent", "Offensive"];

    /// <summary>Verbs that read as goodwill toward whoever they land on.</summary>
    public static readonly string[] Warm = ["Social", "Supportive", "Diplomatic"];

    // ── The player's rings ───────────────────────────────────────────────

    public static readonly ActionCategory Combat = new(
        "Combat", ["Aggressive", "Violent", "Offensive", "Defensive"],
        "force, and standing up to it");

    public static readonly ActionCategory Social = new(
        "Social", ["Social", "Diplomatic", "Supportive", "Persuasive", "Binding"],
        "words, offers, and asking");

    public static readonly ActionCategory Self = new(
        "Self", ["SelfPreserving", "Greedy"],
        "eating, resting, keeping going");

    public static readonly ActionCategory Labor = new(
        "Labor", ["Industrious", "Pride"],
        "the work of the day");

    public static readonly ActionCategory Movement = new(
        "Move", ["Mobile"],
        "closing the distance");

    /// <summary>
    /// Ring one, in the order it is offered. Ordered by how often a narrator
    /// actually reaches for it, not alphabetically — the wheel is muscle memory
    /// and the order has to stop moving.
    /// </summary>
    public static readonly IReadOnlyList<ActionCategory> Categories =
        [Social, Combat, Self, Labor, Movement];

    /// <summary>Every ring this action shows up in. May be more than one.</summary>
    public static IEnumerable<ActionCategory> Of(IAction action)
    {
        foreach (var category in Categories)
            if (category.Contains(action)) yield return category;
    }

    /// <summary>
    /// Verbs with no keyword any ring recognises. Surfaced rather than dropped:
    /// a verb the player cannot reach is a bug, and the only way to notice it
    /// is to have somewhere for it to show up.
    /// </summary>
    public static bool IsUncategorised(IAction action)
    {
        foreach (var category in Categories)
            if (category.Contains(action)) return false;
        return true;
    }

    /// <summary>
    /// Does this action answer that whisper? True when the action carries
    /// EVERY keyword the whisper named.
    /// </summary>
    /// <remarks>
    /// Superset, not overlap, and the difference is the whole expressiveness of
    /// the system. Whispering the general verb names few keywords and therefore
    /// catches its specific cousins too — <c>Converse</c> is
    /// <c>Social, Diplomatic</c>, so asking for it also argues for
    /// <c>Recruit</c> and <c>Speak</c>, which carry those plus one more. Asking
    /// for <c>Recruit</c> names <c>Binding</c> as well and catches only itself.
    /// Breadth falls out of how specific the verb you pointed at was; nothing
    /// had to be authored to make "go and talk to them" vaguer than "ask them
    /// to join us".
    /// <para>
    /// Overlap-matching would collapse that: <c>Attack</c> and <c>Grapple</c>
    /// share <c>Aggressive</c>, so every whisper for one would argue for the
    /// other and specificity would be unreachable.
    /// </para>
    /// </remarks>
    public static bool Answers(IAction action, IReadOnlyList<string> whisperedKeywords)
    {
        if (whisperedKeywords.Count == 0) return true;   // "anything at all"
        foreach (var keyword in whisperedKeywords)
            if (!action.Keywords.Contains(keyword)) return false;
        return true;
    }
}
