using System.Numerics;

namespace Game.Contracts;

/// <summary>
/// The ledger's own taxonomy — what KIND of thing happened, orthogonal to
/// what it was about. Semantics travel on <see cref="WorldEvent.Keywords"/>,
/// never on this enum: kinds answer "how should the world route it", keywords
/// answer "what does it mean to me".
/// </summary>
public enum EventKind
{
    /// <summary>An entity did something. The overwhelming majority.</summary>
    Action,

    /// <summary>Something was said. Dialog renders its own lines.</summary>
    Speech,

    /// <summary>A need crossed a line that hurt (starvation, exhaustion).</summary>
    Need,

    /// <summary>Somebody stopped existing.</summary>
    Death,

    /// <summary>
    /// The narrator talking to the player. Has no place in the world, so it
    /// reaches no witnesses — it is a line in the log and nothing more.
    /// </summary>
    Narration,
}

/// <summary>
/// How an entity came to know about an event. The audience tiers, weakest last.
/// </summary>
/// <remarks>
/// This distinction is the whole point of the distributor. Being punched, seeing
/// someone punched, and doing the punching are three different pieces of
/// information about the same fact, and a reaction that cannot tell them apart
/// will either make bystanders bleed or make the world stop at the edge of the
/// two people involved in it.
/// </remarks>
public enum Perception
{
    /// <summary>You did it.</summary>
    Actor,

    /// <summary>It was aimed at you — you were part of what happened.</summary>
    Participant,

    /// <summary>You were near enough to notice. You are owed nothing.</summary>
    Witness,
}

/// <summary>
/// A fact the world published: who did what, to whom, near where.
/// </summary>
/// <remarks>
/// Machine-readable first. <see cref="Message"/> is a RENDERING of the row that
/// happens to be produced at publish time (the logs and UI have always read
/// prose, and this keeps them working); every system that reasons about an
/// event reads the typed fields instead, and in particular matches
/// <see cref="Keywords"/> — never <see cref="ActionName"/>. That is the same
/// keywords-not-names rule the weight systems live under, one level up.
/// <para>
/// <see cref="ParticipantIds"/> is a LIST rather than a single target because a
/// group is the normal case, not the exceptional one: a conversation, a band
/// travelling together, and a brawl all name several people who were part of
/// what happened. One shape covers all of them, and the distributor fans out
/// over it without needing a second event type.
/// </para>
/// </remarks>
public sealed class WorldEvent
{
    public required int Tick { get; init; }
    public EventKind Kind { get; init; } = EventKind.Action;

    /// <summary>Whose beat this was. Null for events the world itself produced.</summary>
    public string? ActorId { get; init; }

    /// <summary>Everyone else who was PART of it — the group, not the audience.</summary>
    public IReadOnlyList<string> ParticipantIds { get; init; } = [];

    /// <summary>Diagnostics and rendering only. Never match on this.</summary>
    public string? ActionName { get; init; }

    /// <summary>What the event IS, semantically. This is what reactions match.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>
    /// Things named by the event — items, parties, anything descriptor-bearing.
    /// Hearing an object named is how wanting it starts.
    /// </summary>
    public IReadOnlyList<string> ObjectIds { get; init; } = [];

    /// <summary>Where it happened. The witness radius is measured from here.</summary>
    public Vector3 Where { get; init; }

    /// <summary>The rendering. Prose for logs; never parsed back.</summary>
    public required string Message { get; init; }

    /// <summary>
    /// The broadcast tier: distinct, value-changing events earn a speech bubble
    /// and reach witnesses' personal logs. Quiet events still reach every
    /// reaction — loudness governs what is WRITTEN DOWN, not what is noticed.
    /// </summary>
    public bool Loud { get; init; }

    /// <summary>Was this entity part of what happened, rather than near it?</summary>
    public bool Involves(string entityId) =>
        ActorId == entityId || ParticipantIds.Contains(entityId);
}

/// <summary>One member of an event's audience, and how they came by it.</summary>
public readonly record struct EventAudience(Entity Entity, Perception How);

/// <summary>
/// Something an entity does about a fact it just learned.
/// </summary>
/// <remarks>
/// Reactions are the seam this whole spine exists to open. The distributor's
/// job ends at "these entities noticed"; what noticing DOES — surfacing a
/// desire, souring a relationship, pricing an emission against what a mind
/// values — is written here, one reaction per idea, subscribed at composition
/// time. A reaction must be cheap and must not care what order it runs in.
/// </remarks>
public interface IEventReaction
{
    void React(Entity receiver, WorldEvent worldEvent, Perception how);
}
