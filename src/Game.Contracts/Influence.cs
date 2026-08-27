namespace Game.Contracts;

/// <summary>
/// A player-applied nudge that boosts the weight of matching candidates until
/// the entity's next executed action. Stored on Entity.PendingInfluence and
/// consumed by the decision loop.
/// </summary>
/// <remarks>
/// Addressed by KEYWORDS, never by action name. That is not a stylistic
/// preference — <c>ApplyInfluence</c> is a weight source like any other, and the
/// standing rule is that weight sources match keywords so a new verb inherits
/// every modifier for free. Naming the verb also made the player's whole
/// vocabulary a hardcoded string list, which is exactly why the action wheel
/// could not be built until this changed.
/// <para>
/// The keywords come from whatever verb the player pointed at, so the wheel
/// stays legible while the sim stays generic: pick "Attack" and the whisper
/// carries <c>Aggressive, Violent, Offensive</c>. See
/// <see cref="ActionTaxonomy.Answers"/> for why that catches Attack and not
/// Grapple, and why the general verbs are deliberately broader than the
/// specific ones.
/// </para>
/// </remarks>
public class Influence
{
    /// <summary>
    /// What the whisper argues for. A candidate answers it only if its action
    /// carries EVERY one of these. Empty means "anything at all".
    /// </summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>
    /// Optional: WHO to do it to. Null leaves the choice to the sim (the
    /// original untargeted whisper). Set, and the nudge only lands on
    /// candidates aimed at this entity — which is what makes "interact with
    /// THAT one" expressible at all. Reach is not this concern's problem: a
    /// far target simply turns the beat into an Approach, and the intent
    /// survives the walk.
    /// </summary>
    public string? TargetEntityId { get; init; }

    /// <summary>
    /// Optional: WHAT WITH. Null leaves it to the sim, which picks at random
    /// from whatever is viable.
    /// </summary>
    /// <remarks>
    /// The third thing a beat is made of, and until now the only one the player
    /// could not say. "Attack him" and "attack him with the kitchen knife" are
    /// different instructions, and the item was being chosen by a die roll the
    /// narrator had no voice in. A named item that turns out not to be viable
    /// this beat is not an error — the sim falls back to its own pick, because
    /// a whisper is a bid and bids can be impractical.
    /// </remarks>
    public string? ItemId { get; init; }

    public int Strength { get; init; }
    public string Source { get; init; } = "Player";

    /// <summary>
    /// What the player thinks they asked for, for the HUD to echo back.
    /// </summary>
    /// <remarks>
    /// PRESENTATION ONLY. Nothing may ever match on this — it exists because
    /// "standing: Aggressive, Violent, Offensive -> Yorick" is not what anyone
    /// typed, and a player who cannot read their own standing order back has no
    /// way to know whether it is still live.
    /// </remarks>
    public string Label { get; init; } = string.Empty;
}
