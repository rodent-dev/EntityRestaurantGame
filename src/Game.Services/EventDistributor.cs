using System.Numerics;
using Game.Contracts;

namespace Game.Services;

/// <summary>
/// The world's public address system: takes a fact, works out who could have
/// noticed it, and hands it to each of them.
/// </summary>
/// <remarks>
/// This is the piece that makes a world rather than a set of pairs. Before it,
/// an event reached the actor and whoever it was aimed at, and everyone else
/// in the room may as well have been in another building. The audience tiers
/// (<see cref="Perception"/>) keep that from swinging too far the other way:
/// bystanders NOTICE, which is a much weaker thing than being involved, and
/// reactions are told which one they are looking at.
/// <para>
/// Deliberately not a subscriber list per entity, and deliberately not queued.
/// Publishing is synchronous and the audience is walked in roster order, so a
/// seeded run distributes events in exactly the same order every time. Nothing
/// here rolls dice; if a reaction wants randomness it brings its own seeded
/// stream.
/// </para>
/// <para>
/// The witness sweep is a linear scan over the active roster — O(n) per event,
/// which is nothing at settlement scale and is precisely the call site the
/// spatial grid in <c>specs/active/Decision_Appraisal_Design.md</c> §4 replaces
/// when population makes it matter. Keeping it in one method is the point.
/// </para>
/// </remarks>
public sealed class EventDistributor
{
    /// <summary>
    /// How far a thing carries. Generous next to an interaction reach of ~3m:
    /// you can see a fight across a room you could not join.
    /// </summary>
    public const float WitnessRadius = 12f;

    /// <summary>Rows kept in memory. The ledger is a window, not an archive.</summary>
    public const int LedgerCap = 400;

    private readonly List<WorldEvent> _ledger = new();
    private readonly List<IEventReaction> _reactions = new();

    /// <summary>The recent past, oldest first. Read-only to everyone.</summary>
    public IReadOnlyList<WorldEvent> Ledger => _ledger;

    /// <summary>Monotonic count of everything ever published, trimming included.</summary>
    public long TotalPublished { get; private set; }

    /// <summary>
    /// How many times each tier has been reached, counted AT PUBLISH TIME.
    /// </summary>
    /// <remarks>
    /// Recomputing an old row's audience later answers a different question —
    /// people move, and people die — so "did anyone actually witness anything"
    /// is only answerable if the distributor counts as it goes. Diagnostics
    /// only; nothing in the sim reads them.
    /// </remarks>
    public long ReachedActors { get; private set; }
    public long ReachedParticipants { get; private set; }
    public long ReachedWitnesses { get; private set; }

    /// <summary>
    /// Register a reaction. Order of registration is the order they run, which
    /// is fixed at composition time and therefore reproducible.
    /// </summary>
    public void Subscribe(IEventReaction reaction) => _reactions.Add(reaction);

    /// <summary>
    /// Publish a fact and let it land. Returns the audience so the caller —
    /// the controller, which owns the logs — can write down who saw what.
    /// </summary>
    public List<EventAudience> Publish(WorldEvent worldEvent, IReadOnlyList<Entity> roster)
    {
        _ledger.Add(worldEvent);
        TotalPublished++;
        if (_ledger.Count > LedgerCap) _ledger.RemoveAt(0);

        var audience = Audience(worldEvent, roster);

        foreach (var member in audience)
        {
            switch (member.How)
            {
                case Perception.Actor: ReachedActors++; break;
                case Perception.Participant: ReachedParticipants++; break;
                default: ReachedWitnesses++; break;
            }

            foreach (var reaction in _reactions)
                reaction.React(member.Entity, worldEvent, member.How);
        }

        return audience;
    }

    /// <summary>
    /// Who noticed. Everyone named by the event, plus everyone standing near
    /// enough to it; roster order, so the result is deterministic.
    /// </summary>
    /// <remarks>
    /// The dead witness nothing — but a dead ACTOR still gets their own event,
    /// because dying is the one thing you are the protagonist of on your way
    /// out. Narration never reaches witnesses at all: it is the narrator
    /// talking to the player, and the world does not overhear it.
    /// </remarks>
    public List<EventAudience> Audience(WorldEvent worldEvent, IReadOnlyList<Entity> roster)
    {
        var audience = new List<EventAudience>();
        float radiusSq = WitnessRadius * WitnessRadius;

        foreach (var entity in roster)
        {
            if (entity.ID == worldEvent.ActorId)
            {
                audience.Add(new EventAudience(entity, Perception.Actor));
                continue;
            }

            if (!entity.IsAlive) continue;

            if (worldEvent.ParticipantIds.Contains(entity.ID))
            {
                audience.Add(new EventAudience(entity, Perception.Participant));
                continue;
            }

            if (worldEvent.Kind == EventKind.Narration) continue;

            if (Vector3.DistanceSquared(entity.Position, worldEvent.Where) <= radiusSq)
                audience.Add(new EventAudience(entity, Perception.Witness));
        }

        return audience;
    }

    /// <summary>Everything this entity was part of or saw, oldest first.</summary>
    public IEnumerable<WorldEvent> Involving(string entityId) =>
        _ledger.Where(e => e.Involves(entityId));

    /// <summary>Ledger rows carrying any of these keywords. The reaction-author's lens.</summary>
    public IEnumerable<WorldEvent> Tagged(params string[] keywords) =>
        _ledger.Where(e => e.Keywords.Any(keywords.Contains));
}
