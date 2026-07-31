using Game.Contracts;

namespace Game.Services;

/// <summary>
/// Who runs with whom, and who could be talked into it. Owns the party
/// registry and the arithmetic of joining; RecruitAction decides WHEN to ask,
/// this decides whether the answer is yes.
///
/// Recruitment is deliberately not a special player power. It is a contested
/// comparison any entity can attempt against any other, which is what makes
/// bands form out in the world while nobody is watching — the player is just
/// one more recruiter with the same odds.
/// </summary>
public class PartyService
{
    /// <summary>Regard needed before joining is even on the table, before the
    /// party's own appeal moves the bar.</summary>
    public const int BaseRecruitThreshold = 4;

    /// <summary>Relationship points one unit of party appeal is worth. Appeal
    /// buys its way past regard: a band that speaks to what you value needs
    /// less personal warmth to get a yes.</summary>
    public const double AppealToRegard = 3.0;

    /// <summary>Spread of the contested roll, in appeal units. Big enough that
    /// a close call genuinely goes either way, small enough that a bad match
    /// stays a bad match.</summary>
    public const double RollSpread = 2.0;

    /// <summary>What belonging is worth on its own — the inertia a poacher
    /// fights even when the current band is a poor fit.</summary>
    public const double BelongingBonus = 1.5;

    private readonly Dictionary<string, Party> _parties = new();
    private readonly Random _rng;
    private int _nextId = 1;
    private Func<IReadOnlyList<Entity>> _world = () => [];

    public PartyService(Random? rng = null) => _rng = rng ?? new Random();

    /// <summary>Point the service at the live roster (same handshake
    /// DialogService uses), so bond math needs no world argument threaded
    /// through IAction's world-free signatures.</summary>
    public void BindWorld(Func<IReadOnlyList<Entity>> world) => _world = world;

    public IReadOnlyCollection<Party> All => _parties.Values;

    public Party? Get(string? partyId) =>
        partyId is not null && _parties.TryGetValue(partyId, out var p) ? p : null;

    public Party? PartyOf(Entity entity) => Get(entity.PartyId);

    public bool SameParty(Entity a, Entity b) =>
        a.PartyId is not null && a.PartyId == b.PartyId;

    // ── Membership ──────────────────────────────────────────────────────

    /// <summary>
    /// Form a band around a leader. Descriptors default to the leader's own
    /// self-image: a party with no stated character takes the character of
    /// whoever started it, which is why a glory-hound's band reads as a
    /// glory-hound's band to anyone deciding whether to sign on.
    /// </summary>
    public Party Create(string name, Entity leader, IReadOnlyDictionary<string, double>? descriptors = null)
    {
        var party = new Party
        {
            Id = $"party_{_nextId++}",
            Name = name,
            LeaderId = leader.ID,
            Descriptors = descriptors is not null
                ? new Dictionary<string, double>(descriptors)
                : new Dictionary<string, double>(leader.SelfDescriptors),
        };
        _parties[party.Id] = party;
        Add(party, leader);
        return party;
    }

    public void Add(Party party, Entity member)
    {
        if (party.Contains(member.ID)) return;
        Remove(member);
        party.MemberIds.Add(member.ID);
        member.PartyId = party.Id;
    }

    /// <summary>Leave whatever band they are in. A party that loses its last
    /// member is forgotten; one that loses its leader promotes the next in.</summary>
    public void Remove(Entity member)
    {
        if (PartyOf(member) is not { } party) return;

        party.MemberIds.Remove(member.ID);
        member.PartyId = null;

        if (party.MemberIds.Count == 0) _parties.Remove(party.Id);
        else if (party.LeaderId == member.ID) party.LeaderId = party.MemberIds[0];
    }

    public IEnumerable<Entity> MembersOf(Party party) =>
        _world().Where(e => party.Contains(e.ID));

    // ── Would they join? ────────────────────────────────────────────────

    /// <summary>
    /// The band on offer. Someone who already runs with others offers that
    /// band; a loner offers the one they WOULD found, described by their own
    /// self-image. Founding is just recruiting your first member, so drifters
    /// can start crews and the world fills with bands without the player
    /// having to seed the first one.
    /// </summary>
    public Party OfferedParty(Entity recruiter) =>
        PartyOf(recruiter) ?? new Party
        {
            Name = $"{recruiter.DisplayName}'s band",
            LeaderId = recruiter.ID,
            Descriptors = new Dictionary<string, double>(recruiter.SelfDescriptors),
        };

    /// <summary>
    /// How much this party speaks to what the entity values — the preference
    /// sheet dotted against the party's descriptors, exactly as the dialog
    /// system values an item. Positive means "these are my kind of people".
    /// </summary>
    public static double Appeal(Entity candidate, Party party) =>
        party.Descriptors.Sum(d => candidate.Mind.Weight(d.Key) * d.Value);

    /// <summary>
    /// What the entity would be giving up: the appeal of their current band
    /// plus the plain comfort of already belonging, plus how warmly they
    /// regard the people in it. A loner scores zero here — which is precisely
    /// why drifters are recruitable and contented members are not.
    /// </summary>
    public double IncumbentPull(Entity candidate)
    {
        if (PartyOf(candidate) is not { } current) return 0;

        double bonds = MembersOf(current)
            .Where(m => m.ID != candidate.ID)
            .Sum(m => candidate.EntityRelationships.GetValueOrDefault(m.ID))
            / AppealToRegard;

        return Appeal(candidate, current) + BelongingBonus + bonds;
    }

    /// <summary>
    /// The regard a recruiter needs before the question is worth asking. The
    /// bar moves: a party that appeals lowers it, and everything the entity
    /// would be walking away from raises it. Deliberately not a wall — a
    /// compelling enough band can drop the bar to nothing, and no bond makes
    /// someone truly unpoachable.
    /// </summary>
    public int RecruitThreshold(Entity candidate, Party party)
    {
        double shift = (Appeal(candidate, party) - IncumbentPull(candidate)) * AppealToRegard;
        return (int)Math.Round(BaseRecruitThreshold - shift);
    }

    /// <summary>Is this prospect worth asking at all? Gates candidacy, so
    /// Recruit only appears as an option against someone plausibly winnable.</summary>
    public bool IsEligible(Entity candidate, Entity recruiter)
    {
        if (candidate.ID == recruiter.ID || !candidate.IsAlive || !recruiter.IsAlive) return false;
        if (SameParty(candidate, recruiter)) return false;

        int regard = candidate.EntityRelationships.GetValueOrDefault(recruiter.ID);
        return regard >= RecruitThreshold(candidate, OfferedParty(recruiter));
    }

    /// <summary>
    /// The contested roll. Both sides put their case — the offer (party appeal
    /// plus regard for the recruiter) against the incumbent claim — and each
    /// draws noise, so a close match is a real coin toss and a lopsided one
    /// is not. Returns true if the prospect signs on.
    /// </summary>
    public bool TryRecruit(Entity candidate, Entity recruiter)
    {
        if (!IsEligible(candidate, recruiter)) return false;

        double regard = candidate.EntityRelationships.GetValueOrDefault(recruiter.ID) / AppealToRegard;
        double offer = Appeal(candidate, OfferedParty(recruiter)) + regard;
        double incumbent = IncumbentPull(candidate);

        if (offer + _rng.NextDouble() * RollSpread <= incumbent + _rng.NextDouble() * RollSpread)
            return false;

        // Only now does a hypothetical band become a real one: nobody founds
        // a party until someone has actually agreed to join it.
        var party = PartyOf(recruiter) ?? Create($"{recruiter.DisplayName}'s band", recruiter);
        Add(party, candidate);
        return true;
    }
}
