using EntityARestaurantGame.Simulation;
using Engine.Spatial;
using Engine.Ticks;
using Game.Contracts;
using Game.Services;

namespace EntityARestaurantGame;

/// <summary>
/// The game's API over its entities: owns the clock, the timer wheel, and
/// the rosters, and decides WHEN things happen (decision beats, hunger
/// pulses, death sweeps). HOW they happen is EntityService's job. Nothing
/// here polls or compares ticks — every behavior is an event that fires by
/// arriving at its bucket. The controller is also the system the events
/// announce to: it keeps the signal log the UI reads.
/// </summary>
public class EntityController
{
    // ── Pacing ──────────────────────────────────────────────────────────
    public const double TicksPerSecond = 2;        // real-time auto-run speed
    public const int HungerPulseInterval = 10;     // ticks between hunger beats
    // Full belly lasts 500 ticks — slow enough for the work/trade economy to
    // circulate before anyone starves (5/pulse starved settlements out in
    // 200, faster than goods could ever move; soak-verified 2026-07-18).
    public const int HungerPerPulse = 2;
    public const double StarvationDamage = 2;      // HP lost per pulse at 0 hunger
    public const int DecisionInterval = 5;         // ticks between an entity's decisions
    public const int StrangerResistDivisor = 2;    // non-favored pawns heed influence at 1/2
    public const int InfluenceStrength = 12;       // weight one player whisper adds
    private const float WanderStep = 1.0f;

    private const int SignalLogCap = 50;
    private const int EntityLogCap = 30;

    private readonly EntityService _entities;
    private readonly DialogService? _dialog;
    private readonly PartyService? _parties;
    private readonly Clock _clock = new();
    private readonly Wheel _wheel;
    private readonly SimLoop _loop;

    private readonly Dictionary<string, EntityDecisionEvent> _decisionEvents = new();
    private readonly Dictionary<string, Queue<DecisionRecord>> _decisionHistory = new();
    private readonly List<GameSignal> _signals = new();
    private readonly Dictionary<string, Queue<EntityLogEntry>> _entityLogs = new();
    private readonly Dictionary<string, Commitment> _commitments = new();
    private readonly EventDistributor _events = new();

    public List<Entity> ActiveEntities { get; } = new();
    public List<Entity> InactiveEntities { get; } = new();

    public int CurrentTick => _clock.Tick;
    public IReadOnlyList<GameSignal> Signals => _signals;

    /// <summary>
    /// The world's public address system, and the typed ledger behind it.
    /// Subscribe reactions here at composition time; read the ledger for the
    /// machine-readable form of everything the signal log renders as prose.
    /// </summary>
    public EventDistributor Events => _events;

    /// <summary>Monotonic count of every signal ever announced — lets UIs spot
    /// new arrivals even though the log itself trims to its cap.</summary>
    public long TotalSignals { get; private set; }
    public LocationRegistry? Locations => _entities.Locations;

    /// <summary>
    /// The pawn the player-narrator acts through. Strangers (everyone else)
    /// heed influence at reduced strength. Null until the intro picks one.
    /// </summary>
    public string? FavoredEntityId
    {
        get => _favoredEntityId;
        set
        {
            _favoredEntityId = value;
            EnsurePlayerParty();
        }
    }
    private string? _favoredEntityId;

    public Entity? FavoredEntity => ActiveEntities.FirstOrDefault(e => e.ID == FavoredEntityId);

    /// <summary>
    /// Does the narrator's word carry full weight with this entity? Party
    /// members do as asked; everyone else half-listens. This is the mechanical
    /// meaning of belonging, and the reason recruiting is worth doing.
    /// </summary>
    public bool IsOurs(Entity entity) =>
        PlayerParty is { } party ? party.Contains(entity.ID) : entity.ID == FavoredEntityId;

    /// <summary>
    /// The player starts as a band of one. Picking a pawn who belongs to
    /// nobody forms their party around them; picking one who already runs
    /// with others simply adopts that band, which is what makes joining an
    /// existing crew a coherent way to start.
    /// </summary>
    private void EnsurePlayerParty()
    {
        if (_parties is null || FavoredEntity is not { } pawn) return;
        if (_parties.PartyOf(pawn) is not null) return;
        _parties.Create($"{pawn.DisplayName}'s band", pawn);
    }

    /// <summary>The dialog pipeline, exposed for UI hooks (mind inspection, whispers).</summary>
    public DialogService? Dialog => _dialog;

    /// <summary>Party registry, exposed so the UI can draw the roster.</summary>
    public PartyService? Parties => _parties;

    /// <summary>
    /// Optional tap on the decision pipeline: fires once per beat with the full
    /// candidate set and what won. Diagnostics only — the sim runs the same
    /// whether or not anyone is listening. Balance harnesses subscribe here
    /// instead of inferring intent from the signal log, which cannot see the
    /// beats that never announce (Idle, Converse) or the options that lost.
    /// </summary>
    public Action<DecisionTrace>? DecisionObserver { get; set; }

    /// <summary>
    /// The band the player travels with — the favored pawn's party. Everyone
    /// in it is "ours" for influence purposes; everyone else is a stranger.
    /// Null before the intro picks a pawn.
    /// </summary>
    public Party? PlayerParty =>
        FavoredEntity is { } pawn ? _parties?.PartyOf(pawn) : null;

    /// <summary>The player's band as live entities, leader first.</summary>
    public IReadOnlyList<Entity> PlayerPartyMembers =>
        PlayerParty is { } party
            ? [.. ActiveEntities.Where(e => party.Contains(e.ID))
                                .OrderByDescending(e => e.ID == party.LeaderId)]
            : [];

    public EntityController(EntityService entityService, DialogService? dialog = null,
        PartyService? parties = null)
    {
        _entities = entityService;
        _dialog = dialog;
        _parties = parties;
        _dialog?.BindWorld(() => ActiveEntities, () => CurrentTick);
        _wheel = new Wheel(_clock);
        _loop = new SimLoop(_clock, _wheel, TicksPerSecond);
        _wheel.ScheduleIn(new HungerPulseEvent(this), HungerPulseInterval);
    }

    // ── Time ────────────────────────────────────────────────────────────

    /// <summary>Feed real elapsed seconds to the sim. Returns ticks advanced.</summary>
    public int Pump(double realDeltaSeconds) => _loop.Pump(realDeltaSeconds);

    /// <summary>
    /// Manual step: advance time until at least one event actually fires.
    /// Idle ticks are free, so this skips straight to the next beat.
    /// </summary>
    public void AdvanceToNextBeat()
    {
        for (int i = 0; i < Wheel.MaxHorizon; i++)
        {
            int fired = _wheel.ProcessCurrent();
            _clock.Advance();
            if (fired > 0) return;
        }
    }

    // ── Entities ────────────────────────────────────────────────────────

    public string LocationTag(Entity entity) => _entities.LocationTag(entity);

    public IReadOnlyList<DecisionRecord> GetDecisionHistory(string entityId)
    {
        _decisionHistory.TryGetValue(entityId, out var q);
        return q?.ToList() ?? [];
    }

    /// <summary>The entity's personal log — everything it did or had done to
    /// it, newest last. The dossier the inspect UI reads.</summary>
    public IReadOnlyList<EntityLogEntry> GetLog(string entityId)
    {
        _entityLogs.TryGetValue(entityId, out var q);
        return q?.ToList() ?? [];
    }

    /// <summary>Newest LOUD entry where the entity was the protagonist — the
    /// line a speech bubble shows. Allocation-free; safe to call every frame.</summary>
    public EntityLogEntry? LatestBroadcast(string entityId)
    {
        if (!_entityLogs.TryGetValue(entityId, out var q)) return null;
        EntityLogEntry? latest = null;
        foreach (var entry in q)
            if (entry.AsActor && entry.Loud) latest = entry;
        return latest;
    }

    public void AddEntity(Entity entity, bool active = true)
    {
        if (active) ActiveEntities.Add(entity);
        else InactiveEntities.Add(entity);
        _decisionHistory[entity.ID] = new Queue<DecisionRecord>();
        if (active) ScheduleDecisions(entity);
    }

    public void MakeActive(Entity entity)
    {
        if (!InactiveEntities.Remove(entity))
            throw new InvalidOperationException($"Entity '{entity.DisplayName}' is not inactive.");
        ActiveEntities.Add(entity);
        ScheduleDecisions(entity);
    }

    public void MakeInactive(Entity entity)
    {
        if (!ActiveEntities.Remove(entity))
            throw new InvalidOperationException($"Entity '{entity.DisplayName}' is not active.");
        InactiveEntities.Add(entity);

        // Whatever they were in the middle of stops being in the middle of
        // anything. Not settled — dying at your bench is not a shift worked.
        Abandon(entity);

        // Lazy-cancel: the event still reaches its bucket but no-ops on Fire.
        if (_decisionEvents.Remove(entity.ID, out var ev))
            ev.Cancelled = true;
    }

    private void ScheduleDecisions(Entity entity)
    {
        var ev = new EntityDecisionEvent(this, entity, DecisionInterval);
        _decisionEvents[entity.ID] = ev;
        // Stagger first decisions so entities don't act in lockstep.
        _wheel.ScheduleIn(ev, 1 + _decisionEvents.Count % DecisionInterval);
    }

    // ── Event callbacks ─────────────────────────────────────────────────

    internal void OnHungerPulse()
    {
        foreach (var entity in ActiveEntities.ToList())
        {
            if (_entities.ApplyHunger(entity, HungerPerPulse))
            {
                entity.TakeDamage(StarvationDamage);
                Announce($"{entity.DisplayName} is starving (-{StarvationDamage:0} HP)", entity,
                    kind: EventKind.Need, keywords: ["Starving", "Harm"]);
            }
        }
        SweepDeaths();
    }

    internal void ActOnce(Entity entity)
    {
        bool stranger = FavoredEntityId != null && !IsOurs(entity);

        // The challenger: what the weight pipeline offers this beat. Built even
        // when the entity is already busy, because "is anything worth stopping
        // for?" cannot be asked without knowing what "anything" is — and asking
        // it every beat keeps the cost of a beat the same whether the entity is
        // deciding or working.
        var candidates = _entities.BuildCandidates(entity, ActiveEntities);
        ActionCandidate? selected = null;

        if (candidates.Count > 0)
        {
            _entities.ApplyBehaviorWeights(entity, candidates);
            _entities.ApplyStatusWeights(entity, candidates);
            _entities.ApplyProximityWeights(entity, candidates);
            _dialog?.ApplySituationWeights(entity, candidates, CurrentTick);
            _entities.ApplyInfluence(entity, candidates, stranger, StrangerResistDivisor);

            selected = _entities.SelectWeighted(candidates);
        }

        // Already in the middle of something? Then the question this beat is
        // whether the challenger is worth walking out for — not what to do next.
        if (entity.Current is not null && CarryOn(entity, candidates, selected, stranger))
            return;

        if (selected == null) return;

        // Walking out of an activity SETTLES it on the way past, and settling
        // pays out — an abandoned attack still lands its damage. So the target
        // the challenger was chosen against may have died a few lines ago.
        // Committing anyway would spend the next beat discovering the corpse.
        if (selected.Target.ID != entity.ID && !selected.Target.IsAlive) return;

        // Spatial gate: desire is global, reach is local. A far target turns
        // this beat into movement; the intent (influence included) persists
        // until the entity is close enough to actually act. Movement actions
        // are exempt — closing the gap IS what they do, so substituting a
        // step for them would just do the same work twice.
        if (selected.Target.ID != entity.ID &&
            !selected.Action.Keywords.Contains(EntityService.MovementKeyword) &&
            !_entities.IsInReach(entity, selected.Target))
        {
            float remaining = _entities.MoveToward(entity, selected.Target, EntityService.MoveSpeed(entity),
                EntityService.ReachDistance(entity, selected.Target));
            RecordDecision(entity, "Approach", selected.Target.DisplayName, "Distance");
            Announce($"{entity.DisplayName} moves toward {selected.Target.DisplayName} ({remaining:0.0}m away)", entity);
            DecisionObserver?.Invoke(new DecisionTrace(CurrentTick, entity, candidates, selected, Deferred: true));
            _entities.ResolveOverlaps(ActiveEntities);
            return;
        }

        var item = _entities.SelectItem(entity, selected);
        if (item == null && selected.Action.RequiresItem)
        {
            DecisionObserver?.Invoke(new DecisionTrace(CurrentTick, entity, candidates, selected, Deferred: true));
            return;
        }

        bool influenced = entity.PendingInfluence is { } inf
                       && EntityService.MatchesInfluence(selected, inf);
        entity.PendingInfluence = null;   // consumed once an action actually lands

        RecordDecision(entity, selected);
        Begin(entity, selected, item, influenced);
        DecisionObserver?.Invoke(new DecisionTrace(CurrentTick, entity, candidates, selected, Deferred: false));
        FlushDialog();
        SweepDeaths();

        // Personal space: whatever moved this beat (wander, work commute),
        // nobody ends it enveloping someone else.
        _entities.ResolveOverlaps(ActiveEntities);
    }

    // ── Activities: committing, carrying on, settling ───────────────────

    /// <summary>
    /// Commit to a candidate and spend this beat on it. An instantaneous verb
    /// (<see cref="IAction.Duration"/> 1, which is every action that has not
    /// asked for more) begins and settles inside this one call, so it behaves
    /// exactly as it did before activities existed.
    /// </summary>
    private void Begin(Entity entity, ActionCandidate selected, IItem? item, bool influenced)
    {
        var activity = new Activity
        {
            ActionName = selected.Action.ActionName,
            TargetId = selected.Target.ID,
            StartedTick = CurrentTick,
            Item = item,
            Influenced = influenced,
        };
        entity.Current = activity;
        Advance(entity, activity, selected.Action, selected.Target);
    }

    /// <summary>
    /// Spend one beat on an activity: the clock always moves, the score only
    /// moves if the beat counted and the actor had anything to give it.
    /// </summary>
    private void Advance(Entity entity, Activity activity, IAction action, Entity target)
    {
        activity.Elapsed++;
        if (action.Sustain(entity, target, activity.Item))
            activity.Progress += EntityService.Focus(entity);

        if (activity.IsComplete(action.Duration))
            Settle(entity, activity, action, target, completed: true);
    }

    /// <summary>
    /// Spend the beat on whatever the entity is already in the middle of — or
    /// decide it is not worth staying for. Returns true when the beat was spent
    /// here and the caller has nothing left to do.
    /// </summary>
    private bool CarryOn(Entity entity, List<ActionCandidate> candidates,
        ActionCandidate? challenger, bool stranger)
    {
        var current = entity.Current!;
        var action = entity.Actions.GetAction(current.ActionName);
        var target = ActiveEntities.FirstOrDefault(e => e.ID == current.TargetId);

        // The verb or its subject went away underneath us. Nothing to settle:
        // an activity whose target is gone was neither finished nor walked out
        // on, and paying it out either way would be inventing a result.
        if (action is null || target is null || !target.IsAlive)
        {
            Abandon(entity);
            return false;
        }

        // The pipeline landing on what you are ALREADY doing is agreement, not
        // an interruption. Restarting would reset the progress and quietly cap
        // every long action's performance at whatever one beat can earn.
        bool sameThing = challenger is not null
            && challenger.Action.ActionName == current.ActionName
            && challenger.Target.ID == current.TargetId;

        Commitment? commitment = null;
        if (challenger is not null && !sameThing)
        {
            commitment = CommitmentCalculator.Weigh(
                entity, action, current, CurrentTick, StrangerResistDivisor, stranger);
            _commitments[entity.ID] = commitment;

            if (commitment.Total < challenger.TotalWeight)
            {
                Settle(entity, current, action, target, completed: false);
                FlushDialog();
                SweepDeaths();
                return false;                  // the beat goes to whatever won
            }
        }

        Advance(entity, current, action, target);

        RecordDecision(entity, action.ActionName, target.DisplayName,
            ActionWeightSource.Commitment.ToString());
        DecisionObserver?.Invoke(new DecisionTrace(
            CurrentTick, entity, candidates, CarriedOn(action, target, commitment), Deferred: false));

        FlushDialog();
        SweepDeaths();
        _entities.ResolveOverlaps(ActiveEntities);
        return true;
    }

    /// <summary>
    /// Settle a run: pay it out in proportion to how well it went, tidy up
    /// after it, and announce it.
    /// </summary>
    /// <remarks>
    /// Rewards only. Whatever <see cref="IAction.Sustain"/> spent along the way
    /// is already gone and is never refunded or scaled — walk out of a shift
    /// three quarters through and you are still three quarters tired. That
    /// asymmetry is the only reason abandoning something has a cost worth
    /// weighing.
    /// </remarks>
    private void Settle(Entity entity, Activity activity, IAction action, Entity target, bool completed)
    {
        entity.Current = null;
        _commitments.Remove(entity.ID);

        double performance = activity.PerformanceAgainst(action.Duration);
        bool targetWasAlive = target.ID != entity.ID && target.IsAlive;

        action.Resolve(entity, target, activity.Item, performance);
        if (action.ActionName == "Idle")
            _entities.Wander(entity, WanderStep);

        // Spent consumables leave the inventory — depleted husks otherwise
        // linger forever, cluttering loot and food counts.
        if (activity.Item is { UsesRemaining: 0 })
            entity.Inventory.Remove(activity.Item);

        // Loot-on-kill: the killer takes the corpse's belongings. One rule,
        // two fantasies — hunting (wolf drops meat) and banditry (victim
        // drops coin) — with zero special cases.
        if (targetWasAlive && !target.IsAlive && target.Inventory.Count > 0)
        {
            entity.Inventory.AddRange(target.Inventory);
            target.Inventory.Clear();
            Announce($"{entity.DisplayName} loots {target.DisplayName}", entity, target, loud: true,
                keywords: [.. action.Keywords]);
        }

        _entities.ApplyMoodFallout(entity, action, target);
        AnnounceRun(entity, action, target, activity, completed, performance);
    }

    /// <summary>
    /// Drop an activity without settling it — the run's subject stopped
    /// existing, or its owner did. Distinct from walking out, which is a
    /// decision and gets paid for whatever it managed.
    /// </summary>
    private void Abandon(Entity entity)
    {
        entity.Current = null;
        _commitments.Remove(entity.ID);
    }

    /// <summary>
    /// A synthetic candidate standing for "carried on", handed to the decision
    /// observer so a beat spent working counts as a beat spent working.
    /// </summary>
    /// <remarks>
    /// Without it a long action would appear in the census once per RUN rather
    /// than once per beat, and the one number balance work actually needs —
    /// where the beats went — would understate it by the whole duration.
    /// </remarks>
    private static ActionCandidate CarriedOn(IAction action, Entity target, Commitment? commitment)
    {
        var candidate = new ActionCandidate(action, target);
        candidate.Weights.Add(new ActionWeight
        {
            Weight = commitment?.Total ?? CommitmentCalculator.Baseline,
            Source = ActionWeightSource.Commitment,
        });
        return candidate;
    }

    /// <summary>Why this entity stayed on task the last time it was asked.</summary>
    public Commitment? GetCommitment(string entityId) =>
        _commitments.TryGetValue(entityId, out var c) ? c : null;


    /// <summary>
    /// Publish what the dialog pipeline produced this beat: rendered lines go
    /// to the signal log, obsession spikes get their decay event scheduled.
    /// Public so UI-driven whispers can flush immediately.
    /// </summary>
    public void FlushDialog()
    {
        if (_dialog == null) return;

        foreach (var (line, actor, other, loud) in _dialog.DrainSignals())
            Announce(line, actor, other, loud, kind: EventKind.Speech);

        foreach (var (owner, referent) in _dialog.DrainSpikes())
            _wheel.ScheduleIn(new WantSpikeEvent(this, owner, referent), WantSpikeEvent.DecayInterval);
    }

    private void SweepDeaths()
    {
        foreach (var dead in ActiveEntities.Where(e => !e.IsAlive).ToList())
        {
            // The dead leave the band before they leave the board, so a party
            // never lists a corpse and leadership passes on.
            _parties?.Remove(dead);
            MakeInactive(dead);   // also lazy-cancels its decision event
            Announce($"{dead.DisplayName} has died", dead, loud: true,
                kind: EventKind.Death, keywords: ["Death"]);
        }
    }

    // ── Bookkeeping ─────────────────────────────────────────────────────

    private void RecordDecision(Entity entity, ActionCandidate selected)
    {
        var primaryFactor = selected.Weights
            .OrderByDescending(w => w.Weight)
            .FirstOrDefault();

        RecordDecision(entity,
            selected.Action.ActionName,
            selected.Target.DisplayName,
            primaryFactor?.Source.ToString() ?? "None");
    }

    private void RecordDecision(Entity entity, string actionName, string targetName, string factor)
    {
        var record = new DecisionRecord(actionName, targetName, factor);

        if (!_decisionHistory.TryGetValue(entity.ID, out var history))
        {
            history = new Queue<DecisionRecord>();
            _decisionHistory[entity.ID] = history;
        }

        history.Enqueue(record);
        if (history.Count > 3) history.Dequeue();
    }

    /// <summary>
    /// Announce a finished run of an action.
    /// </summary>
    /// <remarks>
    /// Fires on settlement rather than on commitment, so an instantaneous verb
    /// produces exactly the line it always did. A run that took more than one
    /// beat also carries its score, because for those the interesting fact is
    /// not that it happened but how well it went — and "cut short" is the shape
    /// walking out takes in the log.
    /// </remarks>
    private void AnnounceRun(Entity actor, IAction action, Entity target, Activity activity,
        bool completed, double performance)
    {
        if (action.ActionName == "Idle") return;      // resting isn't news
        if (action.ActionName == "Converse") return;  // dialog renders its own lines

        bool self = actor.ID == target.ID;
        string aimedAt = self ? "" : $" → {target.DisplayName}";
        string with = activity.Item != null ? $" ({activity.Item.ItemName})" : "";
        string nudge = activity.Influenced ? "  [influenced]" : "";
        string score = action.Duration <= 1
            ? ""
            : completed ? $" ({performance:0.00})" : $", cut short ({performance:0.00})";

        // Tiering: acting ON someone else (attack, grapple, gift) is a distinct
        // interaction — broadcast it. Self-maintenance (consume, work, defend)
        // stays dossier-only.
        Announce($"{actor.DisplayName}: {action.ActionName}{aimedAt}{with}{score}{nudge}",
            actor, self ? null : target, loud: !self,
            actionName: action.ActionName, keywords: [.. action.Keywords]);
    }

    /// <summary>
    /// Publish a fact to the world: it goes in the typed ledger, gets rendered
    /// into the signal log, reaches the personal log of everyone it was ABOUT,
    /// and is offered to everyone standing near enough to notice.
    /// </summary>
    /// <remarks>
    /// This is the one door into the ledger, which is why every parameter past
    /// the message has a default — the twenty-odd existing call sites keep
    /// working unchanged and get a typed row for free, while the ones that know
    /// something worth matching on say so.
    /// <para>
    /// Witnesses reach the REACTIONS unconditionally but only reach the LOGS
    /// when the event is loud. Noticing and writing it down are different
    /// thresholds: a personal log capped at thirty entries would otherwise fill
    /// with everything anyone did nearby, and the dossier is the one surface
    /// where noise costs the player something.
    /// </para>
    /// </remarks>
    internal void Announce(string message, Entity? actor = null, Entity? other = null,
        bool loud = false, EventKind kind = EventKind.Action,
        string? actionName = null, IReadOnlyList<string>? keywords = null,
        IReadOnlyList<string>? objectIds = null)
    {
        var worldEvent = new WorldEvent
        {
            Tick = CurrentTick,
            Kind = kind,
            ActorId = actor?.ID,
            ParticipantIds = other is not null && other.ID != actor?.ID ? [other.ID] : [],
            ActionName = actionName,
            Keywords = keywords ?? [],
            ObjectIds = objectIds ?? [],
            Where = (actor ?? other)?.Position ?? System.Numerics.Vector3.Zero,
            Message = message,
            Loud = loud,
        };
        Publish(worldEvent);
    }

    /// <summary>
    /// Publish a fact assembled by the caller. The richer door; everything
    /// <see cref="Announce"/> can express, plus a group.
    /// </summary>
    public void Publish(WorldEvent worldEvent)
    {
        _signals.Add(new GameSignal(worldEvent.Tick, worldEvent.Message));
        TotalSignals++;
        if (_signals.Count > SignalLogCap) _signals.RemoveAt(0);

        foreach (var (entity, how) in _events.Publish(worldEvent, ActiveEntities))
        {
            if (how == Perception.Witness && !worldEvent.Loud) continue;
            WriteEntityLog(entity, worldEvent.Message, asActor: how == Perception.Actor, worldEvent.Loud);
        }
    }

    private void WriteEntityLog(Entity? entity, string message, bool asActor, bool loud)
    {
        if (entity is null) return;
        if (!_entityLogs.TryGetValue(entity.ID, out var log))
            _entityLogs[entity.ID] = log = new Queue<EntityLogEntry>();
        log.Enqueue(new EntityLogEntry(CurrentTick, message.Trim(), asActor, loud));
        if (log.Count > EntityLogCap) log.Dequeue();
    }
}
