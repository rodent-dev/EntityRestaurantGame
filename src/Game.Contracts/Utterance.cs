namespace Game.Contracts;

/// <summary>
/// Routing verbs for utterances. The type decides WHICH handler digests the
/// claim-bag; it never changes the math inside the handler. A new type earns
/// its place only if it routes differently — a boast is just SubjectMatter
/// with an arrogant leaf, but a Threat conditions future behavior, so it
/// routes on its own.
/// </summary>
public enum IntentType
{
    SubjectMatter,   // surfaces an object: praise, boast, insult
    Rumor,           // claims about a third party who isn't present (or is!)
    Threat,          // conditional-harm claim, no sweetener
    Offer            // the exchange path: barter / coerce
}

/// <summary>
/// A bag of assertion atoms wearing an intent-type envelope. Intent is
/// canonical; text is a render of it (intent → text, never text → intent).
/// Emission and reception are the same pipeline: a received utterance is
/// deposited as a situation variable that reweights the receiver's next beat.
/// </summary>
public class Utterance
{
    public IntentType Type { get; init; }
    public string SpeakerId { get; init; } = string.Empty;
    public string SpeakerName { get; init; } = string.Empty;
    public string AddresseeId { get; init; } = string.Empty;
    public List<Claim> Claims { get; init; } = new();
    public ExchangeOffer? Offer { get; init; }

    /// <summary>The object this utterance is chiefly about.</summary>
    public string TopicId => Offer?.DemandedItemId ?? Claims.FirstOrDefault()?.ObjectId ?? string.Empty;
}

/// <summary>
/// The exchange payload: barter, coerce, and threat are one loop under
/// different weights. A sweetener present = barter (offer targets a want
/// axis); sweetener absent = coercion (the conditional-harm claim in the
/// envelope targets a fear axis). Stores pointers, never scores — reception
/// recomputes pull and cost from the receiver's own sheet.
/// </summary>
public class ExchangeOffer
{
    /// <summary>What the speaker wants out of the receiver's inventory.</summary>
    public string DemandedItemId { get; init; } = string.Empty;
    public string DemandedItemName { get; init; } = string.Empty;

    /// <summary>What the speaker puts on the table; null means coercion.</summary>
    public string? OfferedItemId { get; init; }
    public string? OfferedItemName { get; init; }

    public bool IsCoercion => OfferedItemId is null;
}
