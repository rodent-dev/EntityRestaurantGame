using Game.Contracts;

namespace Game.Services;

/// <summary>
/// One per-beat weighted selector for talk. Emission and reception are the
/// same pipeline: an incoming utterance is digested (stance gates, credibility,
/// belief/affect movement, desire surfacing) and deposited as a situation
/// variable that reweights the receiver's next emission. There is no dialog
/// state machine — turn-taking, topic-drift, and termination are all weights
/// rising and falling; conversation ends by attrition when response-pull
/// decays below other candidates.
///
/// The service holds no per-conversation state. It buffers rendered signal
/// lines and obsession-spike requests for the controller to drain, because
/// the controller decides WHEN things are published and scheduled.
/// </summary>
public class DialogService
{
    // ── Tunables ─────────────────────────────────────────────────────────
    // Emission selector
    public const double TalkTemperature = 1.5;    // softmax T over utterance scores
    public const double ResponsePullBase = 8.0;   // fresh deposit's reply score
    public const double ResponsePullDecay = 0.4;  // score lost per tick of deposit age
    public const int DepositStaleTicks = 30;      // deposits older than this are dropped
    public const double OfferWantThreshold = 0.5; // want above this flips dialog → exchange
    public const float EarshotRange = 6f;         // broadcast radius (tavern rule)

    // Interpretation (stance gates)
    public const double DoubtK = 3.0;             // logistic steepness
    public const double DoubtTheta = 0.6;         // in-character doubt threshold

    // Exchange reception
    public const double AcceptTemperature = 0.5;  // sigmoid T on (pull − cost)

    // Obsession (the parked primitive, spent here and nowhere else)
    public const double ObsessK = 3.0;
    public const double ObsessTheta = 1.2;
    public const double ObsessPinScale = 2.0;     // pinned want = want-at-surface × this

    // Situation weights (bridge into the action selector)
    public const int ReplyActionWeight = 20;      // fresh deposit's pull toward Converse
    public const int ReplyActionDecayPerTick = 2;
    public const double CovetActionScale = 15.0;  // want → Converse-at-holder weight
    public const double AffinityActionScale = 10.0; // sociability×(value+regard) → invest-in-target weight

    private readonly MindService _minds;
    private readonly Random _rng;
    private readonly List<(string Line, Entity? Actor, Entity? Other, bool Loud)> _pendingSignals = new();
    private readonly List<(Entity Owner, DesireReferent Referent)> _pendingSpikes = new();

    private Func<IReadOnlyList<Entity>> _roster = () => [];
    private Func<int> _tick = () => 0;

    public MindService Minds => _minds;

    public DialogService(MindService minds, Random? rng = null)
    {
        _minds = minds;
        _rng = rng ?? new Random();
    }

    /// <summary>Late-bound world access; the controller owns roster and clock.</summary>
    public void BindWorld(Func<IReadOnlyList<Entity>> roster, Func<int> currentTick)
    {
        _roster = roster;
        _tick = currentTick;
    }

    // ── Drains (controller publishes / schedules) ────────────────────────

    public List<(string Line, Entity? Actor, Entity? Other, bool Loud)> DrainSignals()
    {
        var drained = new List<(string, Entity?, Entity?, bool)>(_pendingSignals);
        _pendingSignals.Clear();
        return drained;
    }

    public List<(Entity Owner, DesireReferent Referent)> DrainSpikes()
    {
        var drained = new List<(Entity, DesireReferent)>(_pendingSpikes);
        _pendingSpikes.Clear();
        return drained;
    }

    // ── Situation weights: the deposited claim reweights the ACTION beat ─

    /// <summary>
    /// Bridges the mind into the action selector: a fresh deposit pulls the
    /// entity toward answering its depositor; a live desire pulls it toward
    /// whoever holds the wanted object. Both recomputed per beat — stored
    /// pointers, recomputed magnitudes.
    /// </summary>
    public void ApplySituationWeights(Entity actor, List<ActionCandidate> candidates, int currentTick)
    {
        var mind = actor.Mind;

        // Stale deposits evaporate — attrition, not termination logic.
        if (mind.Deposited != null && currentTick - mind.DepositedTick > DepositStaleTicks)
            mind.Deposited = null;

        foreach (var candidate in candidates)
        {
            if (!candidate.Action.Keywords.Contains("Social")) continue;

            int weight = 0;

            if (mind.Deposited is { } dep && candidate.Target.ID == dep.SpeakerId)
            {
                int age = currentTick - mind.DepositedTick;
                weight += Math.Max(0, ReplyActionWeight - ReplyActionDecayPerTick * age);
            }

            foreach (var desire in mind.Desires)
            {
                if (!HoldsObject(candidate.Target, desire.ObjectId) || candidate.Target.ID == actor.ID)
                    continue;
                var item = FindItem(desire.ObjectId)?.Item;
                if (item == null) continue;
                double want = _minds.EffectiveWant(actor, desire.ObjectId, item.Descriptors);
                if (want > MindService.DesireThreshold)
                    weight += (int)(want * CovetActionScale);
            }

            // Affinity pull — the investment appetite (befriending, taming,
            // gifting are all the same intent). Sociability (the introvert
            // dial) × [value: Want over the target's descriptor sheet —
            // qualities gained by association — + regard: opinion from
            // accumulated beliefs]. Difficulty rides in as negative-valence
            // descriptors; relation stays its own weight tier. Clamped:
            // admiration is a pull, never a compulsion.
            if (candidate.Target.ID != actor.ID)
            {
                double value = _minds.Want(actor, candidate.Target.SelfDescriptors);
                double regard = _minds.Regard(actor, candidate.Target.ID);
                double pull = mind.Sociability * (value + regard) * AffinityActionScale;
                weight += Math.Clamp((int)pull, -6, 12);
            }

            if (weight > 0)
                candidate.Weights.Add(new ActionWeight { Weight = weight, Source = ActionWeightSource.Situation });
        }
    }

    // ── Emission (§6): build candidates, softmax, deliver ────────────────

    public void Converse(Entity speaker, Entity addressee)
    {
        var candidates = BuildUtterances(speaker, addressee);
        var chosen = MindMath.SoftmaxPick(candidates, TalkTemperature, _rng);
        if (chosen == null) return;

        // Replying consumes the deposit; anything else leaves it to decay.
        if (speaker.Mind.Deposited is { } dep && chosen.TopicId == dep.TopicId)
            speaker.Mind.Deposited = null;

        Deliver(chosen, speaker, addressee);
    }

    private List<(Utterance, double)> BuildUtterances(Entity s, Entity a)
    {
        var candidates = new List<(Utterance, double)>();
        double Noise() => _rng.NextDouble() * 0.5;

        // 1. Response pull: answer what was deposited (agree or rebut from own belief).
        if (s.Mind.Deposited is { } dep && dep.SpeakerId == a.ID && dep.Claims.Count > 0)
        {
            double pull = ResponsePullBase - ResponsePullDecay * (_tick() - s.Mind.DepositedTick);
            if (pull > 0)
            {
                var heard = dep.Claims[0];
                double belief = s.Mind.Belief(heard.ObjectId, heard.Axis);
                // Agree = echo (softened); rebut = assert own belief's direction.
                double valence = Math.Abs(belief) > 0.05 ? Math.Clamp(belief, -1, 1) * 0.6 : heard.Valence * 0.6;
                var reply = new Utterance
                {
                    Type = dep.Type == IntentType.Rumor ? IntentType.Rumor : IntentType.SubjectMatter,
                    SpeakerId = s.ID, SpeakerName = s.DisplayName, AddresseeId = a.ID,
                    Claims = [new Claim(heard.ObjectId, heard.ObjectName, heard.Axis, valence)]
                };
                candidates.Add((reply, pull + Noise()));
            }
        }

        // 2. Inventory = standing candidate pool: each carried item emits
        //    self-claims, gated by salience and the speaker's own axis taste.
        foreach (var item in s.Inventory.Where(i => i.Descriptors.Count > 0))
        {
            var claims = item.Descriptors
                .OrderByDescending(d => Math.Abs(d.Value))
                .Take(2)
                .Select(d => new Claim(item.ItemId, item.ItemName, d.Key, d.Value))
                .ToList();
            double affinity = item.Descriptors.Sum(d => s.Mind.Weight(d.Key) * Math.Abs(d.Value)) * 0.5;
            var u = new Utterance
            {
                Type = IntentType.SubjectMatter,
                SpeakerId = s.ID, SpeakerName = s.DisplayName, AddresseeId = a.ID,
                Claims = claims
            };
            candidates.Add((u, MindService.Salience(item.Descriptors) + affinity + Noise()));
        }

        // 3. Boast: subject-matter about self — same handler, arrogant leaf.
        if (s.SelfDescriptors.Count > 0)
        {
            var top = s.SelfDescriptors.OrderByDescending(d => Math.Abs(d.Value)).First();
            var u = new Utterance
            {
                Type = IntentType.SubjectMatter,
                SpeakerId = s.ID, SpeakerName = s.DisplayName, AddresseeId = a.ID,
                Claims = [new Claim(s.ID, s.DisplayName, top.Key, top.Value)]
            };
            candidates.Add((u, MindService.Salience(s.SelfDescriptors) * 0.8 + s.Mind.Weight(Axes.Glory) * 0.3 + Noise()));
        }

        // 4. Rumor: pass along the strongest thing believed about a third party.
        foreach (var (objectId, sheet) in s.Mind.Beliefs)
        {
            if (objectId == a.ID || objectId == s.ID) continue;
            var subject = _roster().FirstOrDefault(e => e.ID == objectId);
            if (subject == null) continue;   // only gossip about people here

            var strongest = sheet.OrderByDescending(kv => Math.Abs(kv.Value)).FirstOrDefault();
            if (Math.Abs(strongest.Value) < 0.2) continue;

            double valence = Math.Clamp(strongest.Value, -1, 1) * 0.5;
            var u = new Utterance
            {
                Type = IntentType.Rumor,
                SpeakerId = s.ID, SpeakerName = s.DisplayName, AddresseeId = a.ID,
                Claims = [new Claim(objectId, subject.DisplayName, strongest.Key, valence)]
            };
            candidates.Add((u, Math.Abs(strongest.Value) * (1.0 + s.Mind.Weight(strongest.Key) * 0.3) + Noise()));
        }

        // 5. Exchange path (§8): desire crossing threshold selects into offer.
        //    Barter if a sweetener exists; coerce if hostile or empty-handed.
        int rel = s.EntityRelationships.GetValueOrDefault(a.ID);
        bool aggressive = s.Behaviors.Any(b => b.Keywords.Contains("Aggressive") && b.Affect > 0);
        foreach (var prize in a.Inventory.Where(i => i.Descriptors.Count > 0))
        {
            double want = _minds.EffectiveWant(s, prize.ItemId, prize.Descriptors);
            if (want < OfferWantThreshold) continue;

            var sweetener = s.Inventory
                .Where(i => i.Descriptors.Count > 0 && i.ItemId != prize.ItemId)
                .OrderBy(i => _minds.KeepValue(s, i))
                .FirstOrDefault();

            bool coerce = sweetener == null || (rel < 0 && aggressive);
            if (coerce && !aggressive) continue;   // meek and empty-handed: just keep talking

            var u = new Utterance
            {
                Type = coerce ? IntentType.Threat : IntentType.Offer,
                SpeakerId = s.ID, SpeakerName = s.DisplayName, AddresseeId = a.ID,
                Claims = coerce ? new List<Claim> { new(a.ID, a.DisplayName, Axes.Harm, 0.4) } : new List<Claim>(),
                Offer = new ExchangeOffer
                {
                    DemandedItemId = prize.ItemId, DemandedItemName = prize.ItemName,
                    OfferedItemId = coerce ? null : sweetener!.ItemId,
                    OfferedItemName = coerce ? null : sweetener!.ItemName
                }
            };
            candidates.Add((u, want * (coerce ? 3.0 : 4.0) + Noise()));
        }

        // 6. Pure threat: hostility with no prize in mind.
        if (rel <= -5)
        {
            var u = new Utterance
            {
                Type = IntentType.Threat,
                SpeakerId = s.ID, SpeakerName = s.DisplayName, AddresseeId = a.ID,
                Claims = [new Claim(a.ID, a.DisplayName, Axes.Harm, 0.4)]
            };
            candidates.Add((u, Math.Abs(rel) * 0.4 + Noise()));
        }

        // Fallback: pleasantries keyed to the speaker's top taste, so talk
        // always seeds SOME belief and the pipeline never stalls.
        if (candidates.Count == 0)
        {
            var topAxis = s.Mind.Preferences.OrderByDescending(p => p.Value).FirstOrDefault().Key ?? Axes.Comfort;
            var u = new Utterance
            {
                Type = IntentType.SubjectMatter,
                SpeakerId = s.ID, SpeakerName = s.DisplayName, AddresseeId = a.ID,
                Claims = [new Claim(a.ID, a.DisplayName, topAxis, 0.2)]
            };
            candidates.Add((u, 1.0));
        }

        // A nonverbal addressee only receives tone — drop pure-language
        // candidates (rumors, boasts) and fall back to wordless soothing so
        // the attempt is never wasted. Offers and threats stay: both work
        // without words.
        if (!Understands(a))
        {
            candidates.RemoveAll(c => c.Item1.Offer is null && c.Item1.Type != IntentType.Threat);
            if (candidates.Count == 0)
                candidates.Add((new Utterance
                {
                    Type = IntentType.SubjectMatter,
                    SpeakerId = s.ID, SpeakerName = s.DisplayName, AddresseeId = a.ID,
                }, 1.5));
        }

        return candidates;
    }

    // ── Delivery (§10): broadcast to all proximate, each digests own way ─

    private void Deliver(Utterance u, Entity speaker, Entity addressee)
    {
        Signal(RenderUtterance(u, addressee), speaker, addressee, loud: true);

        foreach (var witness in _roster())
        {
            if (witness.ID == speaker.ID || !witness.IsAlive) continue;
            bool addressed = witness.ID == addressee.ID;
            // Nonverbal creatures ignore overheard language, but being
            // ADDRESSED reaches them — as tone, not words (see Digest).
            if (!Understands(witness) && !addressed) continue;
            if (!addressed && Distance(speaker, witness) > EarshotRange) continue;
            Digest(u, speaker, witness, addressed);
        }
    }

    /// <summary>
    /// Player rumor injection — runs the identical digestion pipeline. The
    /// whisper is anonymous unless spoken through a pawn, so credibility
    /// falls back to the receiver's neutral trust.
    /// </summary>
    public void Whisper(Entity receiver, Claim claim, Entity? throughPawn)
    {
        var u = new Utterance
        {
            Type = IntentType.Rumor,
            SpeakerId = throughPawn?.ID ?? "narrator",
            SpeakerName = throughPawn?.DisplayName ?? "a whisper",
            AddresseeId = receiver.ID,
            Claims = [claim]
        };
        Signal($"{u.SpeakerName} → {receiver.DisplayName}: {DescribeClaim(claim)}", throughPawn, receiver, loud: true);
        Digest(u, throughPawn, receiver, addressed: throughPawn != null);
    }

    // ── Digestion: interpretation, credibility, movement, desire ─────────

    private void Digest(Utterance u, Entity? speaker, Entity receiver, bool addressed)
    {
        if (u.Offer != null)
        {
            // Offers work without words — food held out to a wolf is an offer
            // it fully understands (Want and menace never needed language).
            if (addressed) ReceiveOffer(u, speaker!, receiver);
            else WitnessOffer(u, receiver);
            return;
        }

        // Nonverbal receivers get the tone read instead of the claims path.
        if (!Understands(receiver))
        {
            DigestTone(u, speaker, receiver);
            return;
        }

        double netFeeling = 0;
        foreach (var claim in u.Claims)
            netFeeling += DigestClaim(u, speaker, receiver, claim);

        // Feeling lands on mood; strong feeling colors the speaker too.
        if (netFeeling != 0)
            receiver.Needs.AdjustMood(Math.Clamp((int)Math.Round(netFeeling * 4), -3, 3));
        if (speaker != null && Math.Abs(netFeeling) > 0.2)
        {
            receiver.EntityRelationships.TryAdd(speaker.ID, 0);
            receiver.EntityRelationships[speaker.ID] += netFeeling > 0 ? 1 : -1;
        }

        // Desire surfacing: hearing about an item runs the want counterfactual.
        foreach (var claim in u.Claims)
            SurfaceDesire(receiver, claim.ObjectId, $"heard from {u.SpeakerName}");

        // The deposit: only the person spoken TO carries the pull to answer.
        if (addressed && speaker != null)
        {
            receiver.Mind.Deposited = u;
            receiver.Mind.DepositedTick = _tick();
        }
    }

    /// <summary>
    /// The nonverbal channel: creatures read menace and warmth, never words.
    /// Threats condition fear exactly like the verbal path; anything else is
    /// a soothing attempt whose odds scale with the speaker's beast-affinity
    /// (Wisdom). Repeated soothing walks the relationship up — taming is this
    /// drip plus the relationship curve suppressing aggression, not a switch.
    /// </summary>
    private void DigestTone(Utterance u, Entity? speaker, Entity receiver)
    {
        if (u.Type == IntentType.Threat && speaker != null)
        {
            receiver.Mind.NudgeBelief(speaker.ID, Axes.Harm, 0.2);
            receiver.Needs.AdjustMood(-3);
            Bump(receiver, speaker, -1);
            return;
        }

        if (speaker == null) return; // a disembodied whisper means nothing to a beast

        if (_rng.NextDouble() < 0.35 * BeastAffinity(speaker))
        {
            Bump(receiver, speaker, +1);
            receiver.Needs.AdjustMood(+1);
            Signal($"  the {receiver.DisplayName.ToLowerInvariant()} settles at {speaker.DisplayName}'s tone", receiver, speaker, loud: true);
        }
    }

    /// <summary>
    /// One claim through the stance gates (§5) and the credibility read (§4).
    /// Returns the receiver's feeling about the accepted content (0 if rejected).
    /// </summary>
    private double DigestClaim(Utterance u, Entity? speaker, Entity receiver, Claim claim)
    {
        var mind = receiver.Mind;
        double belief = mind.Belief(claim.ObjectId, claim.Axis);
        double m = Math.Abs(claim.Valence - belief);            // belief mismatch
        double trust = TrustToward(receiver, speaker);

        // Stance selection: {accept, doubt-subject, doubt-speaker}.
        double pDoubt = MindMath.Sigmoid(DoubtK * (m * mind.Skepticism - DoubtTheta));
        if (_rng.NextDouble() < pDoubt)
        {
            double pBlameSpeaker = Math.Clamp(m * (1.0 - trust), 0, 1);
            if (speaker != null && _rng.NextDouble() < pBlameSpeaker)
            {
                // Rejection reflects onto the speaker instead of landing.
                double sting = (1.0 - trust) * Math.Abs(mind.Weight(claim.Axis) * claim.Valence);
                int hit = -Math.Max(1, (int)Math.Round(sting * 3));
                receiver.EntityRelationships.TryAdd(speaker.ID, 0);
                receiver.EntityRelationships[speaker.ID] += hit;
                Signal($"  {receiver.DisplayName} turns on {speaker.DisplayName} for saying it ({hit})", receiver, speaker, loud: true);
            }
            else
            {
                Signal($"  {receiver.DisplayName} doubts that", receiver);
            }
            return 0;
        }

        // Credibility: no global dial — read from the receiver's relation to
        // speaker (trust) and to the object (mismatch with held belief).
        double cred = trust * MindMath.Sigmoid(1.5 - m);
        double delta = cred * claim.Valence;
        mind.NudgeBelief(claim.ObjectId, claim.Axis, delta);

        if (Math.Abs(delta) >= 0.25)
            Signal($"  {receiver.DisplayName} takes it to heart ({claim.ObjectName}: {claim.Axis} {delta:+0.00;-0.00})", receiver);

        // Feeling: the operator over this accepted claim; claims about the
        // receiver itself weigh double (it's personal).
        double feeling = mind.Weight(claim.Axis) * claim.Valence * 0.1;
        if (claim.ObjectId == receiver.ID) feeling *= 2;

        // Threats condition future behavior: fear belief about the speaker.
        if (u.Type == IntentType.Threat && speaker != null)
        {
            mind.NudgeBelief(speaker.ID, Axes.Harm, 0.2);
            receiver.Needs.AdjustMood(-3);
        }

        return feeling;
    }

    // ── Desire (§9, §11, §12) ────────────────────────────────────────────

    /// <summary>
    /// If the object is a wantable item in the world, run the counterfactual;
    /// past the threshold, store the referent (pointer, never score). New
    /// surfacings roll the rare obsession gate.
    /// </summary>
    private void SurfaceDesire(Entity receiver, string objectId, string context)
    {
        var found = FindItem(objectId);
        if (found == null || found.Value.Holder.ID == receiver.ID) return;

        var descriptors = found.Value.Item.Descriptors;
        double want = _minds.Want(receiver, descriptors);
        if (want < MindService.DesireThreshold || receiver.Mind.FindDesire(objectId) != null)
            return;

        var referent = new DesireReferent
        {
            ObjectId = objectId,
            ObjectName = found.Value.Item.ItemName,
            FirstSurfacedTick = _tick(),
            Context = context
        };
        receiver.Mind.Desires.Add(referent);
        Signal($"  {receiver.DisplayName} covets the {referent.ObjectName} (want {want:0.00})", receiver);

        // Obsession: trait-gated, low-probability, pins want, decays on the wheel.
        double salience = MindService.Salience(descriptors);
        double pObsess = MindMath.Sigmoid(ObsessK * (receiver.Mind.Impulsiveness * salience - ObsessTheta));
        if (_rng.NextDouble() < pObsess)
        {
            referent.PinnedWant = want * ObsessPinScale;
            _pendingSpikes.Add((receiver, referent));
            Signal($"  ★ {receiver.DisplayName} becomes OBSESSED with the {referent.ObjectName}", receiver, loud: true);
        }
    }

    // ── The unified exchange loop (§8) ───────────────────────────────────

    private void ReceiveOffer(Utterance u, Entity speaker, Entity receiver)
    {
        var offer = u.Offer!;
        var demanded = receiver.Inventory.FirstOrDefault(i => i.ItemId == offer.DemandedItemId);
        if (demanded == null) return;   // the prize moved on; offer fizzles

        double cost = _minds.KeepValue(receiver, demanded);
        double pull;

        if (offer.IsCoercion)
        {
            // Fear axis: menace scales with the speaker's ACTUAL harm pile
            // plus what the receiver already believes about them — cunning
            // from relations, not a difficulty slider.
            double menace = MindMath.Sigmoid(
                _minds.OwnedOnAxis(speaker, Axes.Harm)
                + receiver.Mind.Belief(speaker.ID, Axes.Harm) - 1.0) * 2.0;
            pull = receiver.Mind.Weight(Axes.Safety) * 0.3 * menace;
        }
        else
        {
            var sweetener = speaker.Inventory.FirstOrDefault(i => i.ItemId == offer.OfferedItemId);
            if (sweetener == null) return;
            pull = _minds.Want(receiver, sweetener.Descriptors) * (0.5 + TrustToward(receiver, speaker));
        }

        bool accepted = _rng.NextDouble() < MindMath.Sigmoid((pull - cost) / AcceptTemperature);

        if (accepted)
        {
            receiver.Inventory.Remove(demanded);
            speaker.Inventory.Add(demanded);
            speaker.Mind.Desires.RemoveAll(d => d.ObjectId == demanded.ItemId);

            if (offer.IsCoercion)
            {
                Bump(receiver, speaker, -5);
                receiver.Mind.NudgeBelief(speaker.ID, Axes.Harm, 0.3);
                receiver.Needs.AdjustMood(-8);
                Signal($"  {receiver.DisplayName} hands over the {demanded.ItemName}, seething", receiver, speaker, loud: true);
            }
            else
            {
                var sweetener = speaker.Inventory.First(i => i.ItemId == offer.OfferedItemId);
                speaker.Inventory.Remove(sweetener);
                receiver.Inventory.Add(sweetener);
                Bump(receiver, speaker, +2);
                Bump(speaker, receiver, +2);
                Signal($"  deal struck: {demanded.ItemName} for {sweetener.ItemName}", speaker, receiver, loud: true);
            }
        }
        else
        {
            if (offer.IsCoercion)
            {
                Bump(receiver, speaker, -3);
                Bump(speaker, receiver, -3);
                receiver.Mind.NudgeBelief(speaker.ID, Axes.Harm, 0.2);
                receiver.Needs.AdjustMood(-4);
                Signal($"  {receiver.DisplayName} refuses to be strong-armed", receiver, speaker, loud: true);
            }
            else
            {
                Bump(speaker, receiver, -1);
                Signal($"  {receiver.DisplayName} declines the trade", receiver, speaker);
            }
        }
    }

    /// <summary>
    /// Open-encounter seed: a bystander digesting someone else's offer just
    /// learns the prize exists — their own want does the rest next beat.
    /// </summary>
    private void WitnessOffer(Utterance u, Entity witness) =>
        SurfaceDesire(witness, u.Offer!.DemandedItemId, $"overheard {u.SpeakerName}'s offer");

    // ── Rendering: intent → text, never text → intent ────────────────────

    private static string RenderUtterance(Utterance u, Entity addressee)
    {
        string arrow = $"{u.SpeakerName} → {addressee.DisplayName}";
        if (u.Offer is { } offer)
            return offer.IsCoercion
                ? $"{arrow}: demands the {offer.DemandedItemName} — or else"
                : $"{arrow}: offers the {offer.OfferedItemName} for the {offer.DemandedItemName}";

        if (u.Type == IntentType.Threat)
            return $"{arrow}: growls a threat";

        var claim = u.Claims.FirstOrDefault();
        if (claim == null) return $"{arrow}: makes pleasantries";

        if (claim.ObjectId == u.SpeakerId)
            return $"{arrow}: boasts of their {claim.Axis} ({claim.Valence:+0.0;-0.0})";
        if (u.Type == IntentType.Rumor)
            return $"{arrow}: spreads word of {claim.ObjectName} ({claim.Axis} {claim.Valence:+0.0;-0.0})";
        if (claim.ObjectId == addressee.ID)
            return claim.Valence >= 0
                ? $"{arrow}: flatters them ({claim.Axis} {claim.Valence:+0.0})"
                : $"{arrow}: insults them ({claim.Axis} {claim.Valence:-0.0})";

        return claim.Valence >= 0
            ? $"{arrow}: talks up the {claim.ObjectName} ({claim.Axis} {claim.Valence:+0.0})"
            : $"{arrow}: runs down the {claim.ObjectName} ({claim.Axis} {claim.Valence:-0.0})";
    }

    private static string DescribeClaim(Claim c) =>
        $"a rumor about {c.ObjectName} ({c.Axis} {c.Valence:+0.0;-0.0})";

    // ── Small helpers ────────────────────────────────────────────────────

    /// <summary>Trust read from the standing relation — varies per receiver for free.</summary>
    private static double TrustToward(Entity receiver, Entity? speaker) =>
        speaker == null
            ? 0.5
            : MindMath.Sigmoid(receiver.EntityRelationships.GetValueOrDefault(speaker.ID) / 4.0);

    /// <summary>Language users have the Converse action; everyone else reads tone.</summary>
    private static bool Understands(Entity e) => e.Actions.HasAction("Converse");

    /// <summary>How convincing this speaker is to creatures — Wisdom's job.
    /// 10 is a neutral 1.0; a wise handler lands soothing far more often.</summary>
    private static double BeastAffinity(Entity speaker) => speaker.Stats.Wisdom / 10.0;

    private static void Bump(Entity from, Entity toward, int delta)
    {
        from.EntityRelationships.TryAdd(toward.ID, 0);
        from.EntityRelationships[toward.ID] += delta;
    }

    private static bool HoldsObject(Entity e, string objectId) =>
        e.Inventory.Any(i => i.ItemId == objectId);

    private (Entity Holder, IItem Item)? FindItem(string objectId)
    {
        foreach (var e in _roster())
        {
            var item = e.Inventory.FirstOrDefault(i => i.ItemId == objectId);
            if (item != null) return (e, item);
        }
        return null;
    }

    private static float Distance(Entity a, Entity b) =>
        System.Numerics.Vector3.Distance(a.Position, b.Position);

    /// <summary>Actor = the line's protagonist; other just lived it. Loud =
    /// broadcast tier (speech bubbles); quiet lines are dossier-only.</summary>
    private void Signal(string line, Entity? actor = null, Entity? other = null, bool loud = false) =>
        _pendingSignals.Add((line, actor, other, loud));
}
