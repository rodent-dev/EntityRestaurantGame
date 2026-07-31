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

    public List<Entity> ActiveEntities { get; } = new();
    public List<Entity> InactiveEntities { get; } = new();

    public int CurrentTick => _clock.Tick;
    public IReadOnlyList<GameSignal> Signals => _signals;

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
                Announce($"{entity.DisplayName} is starving (-{StarvationDamage:0} HP)", entity);
            }
        }
        SweepDeaths();
    }

    internal void ActOnce(Entity entity)
    {
        var candidates = _entities.BuildCandidates(entity, ActiveEntities);
        if (candidates.Count == 0) return;

        _entities.ApplyBehaviorWeights(entity, candidates);
        _entities.ApplyStatusWeights(entity, candidates);
        _entities.ApplyProximityWeights(entity, candidates);
        _dialog?.ApplySituationWeights(entity, candidates, CurrentTick);

        bool stranger = FavoredEntityId != null && !IsOurs(entity);
        _entities.ApplyInfluence(entity, candidates, stranger, StrangerResistDivisor);

        var selected = _entities.SelectWeighted(candidates);
        if (selected == null) return;

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
            _entities.ResolveOverlaps(ActiveEntities);
            return;
        }

        bool influenced = entity.PendingInfluence is { } inf
                       && EntityService.MatchesInfluence(selected, inf);
        entity.PendingInfluence = null;   // consumed once an action actually lands

        var item = _entities.SelectItem(entity, selected);
        if (item == null && selected.Action.RequiresItem) return;

        bool targetWasAlive = selected.Target.ID != entity.ID && selected.Target.IsAlive;
        selected.Action.Execute(entity, selected.Target, item);
        if (selected.Action.ActionName == "Idle")
            _entities.Wander(entity, WanderStep);

        // Spent consumables leave the inventory — depleted husks otherwise
        // linger forever, cluttering loot and food counts.
        if (item is { UsesRemaining: 0 })
            entity.Inventory.Remove(item);

        // Loot-on-kill: the killer takes the corpse's belongings. One rule,
        // two fantasies — hunting (wolf drops meat) and banditry (victim
        // drops coin) — with zero special cases.
        if (targetWasAlive && !selected.Target.IsAlive && selected.Target.Inventory.Count > 0)
        {
            entity.Inventory.AddRange(selected.Target.Inventory);
            selected.Target.Inventory.Clear();
            Announce($"{entity.DisplayName} loots {selected.Target.DisplayName}", entity, selected.Target, loud: true);
        }

        _entities.ApplyMoodFallout(entity, selected);
        RecordDecision(entity, selected);
        AnnounceDecision(entity, selected, item, influenced);
        FlushDialog();
        SweepDeaths();

        // Personal space: whatever moved this beat (wander, work commute),
        // nobody ends it enveloping someone else.
        _entities.ResolveOverlaps(ActiveEntities);
    }

    /// <summary>
    /// Publish what the dialog pipeline produced this beat: rendered lines go
    /// to the signal log, obsession spikes get their decay event scheduled.
    /// Public so UI-driven whispers can flush immediately.
    /// </summary>
    public void FlushDialog()
    {
        if (_dialog == null) return;

        foreach (var (line, actor, other, loud) in _dialog.DrainSignals())
            Announce(line, actor, other, loud);

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
            Announce($"{dead.DisplayName} has died", dead, loud: true);
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

    private void AnnounceDecision(Entity actor, ActionCandidate selected, IItem? item, bool influenced)
    {
        if (selected.Action.ActionName == "Idle") return;      // resting isn't news
        if (selected.Action.ActionName == "Converse") return;  // dialog renders its own lines

        bool self = actor.ID == selected.Target.ID;
        string target = self ? "" : $" → {selected.Target.DisplayName}";
        string with = item != null ? $" ({item.ItemName})" : "";
        string nudge = influenced ? "  [influenced]" : "";
        // Tiering: acting ON someone else (attack, grapple, gift) is a distinct
        // interaction — broadcast it. Self-maintenance (consume, work, defend)
        // stays dossier-only.
        Announce($"{actor.DisplayName}: {selected.Action.ActionName}{target}{with}{nudge}",
            actor, self ? null : selected.Target, loud: !self);
    }

    /// <summary>Publish to the world log, and to the personal logs of anyone
    /// involved — <paramref name="actor"/> is the entry's protagonist,
    /// <paramref name="other"/> just lived it. <paramref name="loud"/> is the
    /// broadcast tier: only loud entries earn a speech bubble.</summary>
    internal void Announce(string message, Entity? actor = null, Entity? other = null, bool loud = false)
    {
        _signals.Add(new GameSignal(CurrentTick, message));
        TotalSignals++;
        if (_signals.Count > SignalLogCap) _signals.RemoveAt(0);

        WriteEntityLog(actor, message, asActor: true, loud);
        if (other?.ID != actor?.ID) WriteEntityLog(other, message, asActor: false, loud);
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
