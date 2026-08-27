# Activities & the Event Spine

Two patterns ported from **Luna** (`C:\dev\luna`, the sibling project) and
landed 2026-08-26. Luna is where both were designed and measured; this document
records what came across, what changed in translation, and what was left open
on purpose.

The two are independent — either could have shipped alone — but they arrived
together because they answer the same complaint from two directions. An entity
that decides afresh every beat and only ever hears about things aimed at it is
not living in a world; it is a coin being flipped in a room by itself.

---

## 1. Long-running actions — a job as a dominant action

### The problem

Every verb was instantaneous. `IAction.Execute` ran once and the beat was over,
so "work a shift" could only be modelled as *one beat that produces one loaf*,
repeated. Nothing accumulated, nothing could be walked out on, and nothing paid
differently for having been done well. There was no way to say **"how long you
did it decides what you get."**

### The shape

Three default interface members on `IAction`. Defaults are the whole trick:
an action that says nothing is a one-beat action and behaves exactly as it did
before, so nothing needed rewriting to inherit the generic machinery.

```csharp
int Duration => 1;
bool Sustain(Entity user, Entity target, IItem? item) => true;
void Resolve(Entity user, Entity target, IItem? item, double performance)
    => Execute(user, target, item);
```

- **`Duration`** — in *decision beats*, not ticks. A beat is the unit at which
  an entity re-asks what it is doing, so a duration in beats is a duration in
  decisions, which is the number that actually matters: it is how many chances
  the actor gets to wander off.
- **`Sustain`** — one beat of being in the middle of it: the commute, the
  drain, the grind. Returns false for a beat that kept the actor busy without
  moving the work forward.
- **`Resolve`** — settle and pay out, scaled by performance. Never refunds what
  `Sustain` spent.

The run in progress is `Entity.Current` — a plain-data `Activity` carrying
`(ActionName, TargetId, Item, StartedTick, Elapsed, Progress)`.

### Elapsed vs Progress — the load-bearing split

Two numbers, deliberately.

- **`Elapsed`** is wall clock: beats eaten, however well or badly.
- **`Progress`** is beats that *counted*, and rises by
  `EntityService.Focus(actor)` rather than by 1.

Their ratio is `PerformanceAgainst(duration)`, and that is what an action gets
paid on. Three separate things shrink a payout, and none of them is a special
case — they are all just Progress failing to keep up with Elapsed:

| What happened | Mechanism |
|---|---|
| Walked out early | fewer beats elapsed |
| Lives across the settlement | commuting beats `Sustain`-return false, banking nothing |
| Turned up starving | `Focus` below 1, so every beat banks a fraction |

`Focus` is worst-of over hunger / mood / health against the same 0.50 comfort
line `StatusWeightCalculator` uses, floored at 0.25. Worst-of rather than a
sum: one thing being badly wrong is what wrecks concentration, and three mild
complaints are not a crisis.

The direction of that degradation is the point. Being wrecked degrades **output
per beat**; it does not reduce the number of beats spent. A starving cook still
stands the whole shift and simply banks less of it, so the shift resolves badly
and pays badly — a consequence the sim can *show*, where "skips work when
hungry" is only an absence.

### Commitment — is anything worth stopping for?

`CommitmentCalculator.Weigh` prices the incumbent activity's case for being left
alone, and the controller **compares** it against the challenger's total weight.

Never a candidate in the roll. That is not stylistic: a shift weighed 60 against
food weighed 20 would be abandoned with probability 0.25 on *every* beat it is
re-asked, and across a whole shift that is a certainty rather than a risk.
Per-beat rolls compound; thresholds do not. (Luna hit this measured — 93% of all
activities ended in someone walking out, at a mean performance of 0.07.)

Terms, and the reasons they exist:

| Term | Weight | When |
|---|---|---|
| `Baseline` | +4 | always |
| `Momentum` | +18 | ramps over the first quarter of the duration, then flat |
| `SettlingPeak` | +10, −3/beat | covers the start, where momentum is weakest |
| `NearlyThere` | +6 | past 75% elapsed |
| `DistressMax` | up to −18 | worst need below the 25% critical line |
| `AddressedBy` | −6 | an un-stale utterance sits in `Mind.Deposited` |
| `WhisperPull` | −10 (÷2 stranger) | a player whisper the activity does not answer |

`Momentum` is flat rather than rising because what is being defended against is
N independent chances to wander off, not one decision at the midpoint. It is
**not** sunk cost — partial credit means the beats already worked are already
banked and walking out forfeits nothing earned. What finishing buys is the
performance score, and momentum is that pull made legible.

`SettlingPeak` was added after measuring: without it, entities committed to a
shift and left one beat later, producing runs scored `0.00` — a whole beat
spent to buy nothing. Momentum is weakest exactly where it is needed most, and
settling covers that gap from the other end as momentum ramps in.

Two guards that are easy to miss and expensive to omit:

- **Agreement is not interruption.** If the pipeline lands on the same verb and
  target the actor is already doing, carry on without weighing — restarting
  would reset Progress and quietly cap every long action's performance at
  whatever a single beat can earn.
- **A whisper the activity already answers is silent.** The player asking for
  what you are already doing is agreement, not a prod.

### WorkAction as the exemplar

A shift is authored as a **total** — `ShiftBeats = 8`, `ShiftYield = 4`, "eight
beats on the bench makes four loaves" — and the sim divides. Authoring a
per-beat rate instead would make the sentence you actually want to write
unspeakable.

Measured, seed 42 / sim 3, 3000 ticks (`SoakTest -v | grep ": Work"`):

```
t   54  Ida: Work (1.00)                 full shift, sharp    → 4 goods
t  274  Ida: Work (0.99)
t  530  Finn: Work (0.78)                full shift, worn     → 3 goods
t  199  Ida: Work, cut short (0.50)      walked out halfway   → 2 goods
t  610  Finn: Work, cut short (0.05)     starving; bailed     → 0 goods
```

That is the whole ask: how long you did it decides what you get.

### What it cost

Nothing measurable. Across seeds 1–4 at 3000 ticks, survival is unchanged
(1/0/1/2 against a 1/1/1/1 baseline — the settlement collapses to violence
either way, which is the pre-existing P2.22/P2.23 problem), and violence at
t1500 is consistently **lower** (222/223/243/254 vs 239/288/300/276): beats
spent committed to a shift are beats not spent brawling. `DecisionProbe
--selftest` still passes, because commitment rolls no dice.

---

## 2. The event spine — a world, not a set of pairs

### The problem

`EntityController.Announce` took a string and reached exactly two people: the
actor, and whoever the action was aimed at. Everyone else in the room may as
well have been in another building. The one exception was hardcoded inside the
dialog pipeline (`DialogService.WitnessOffer`), which is precisely the shape of
a general idea trapped in one subsystem.

### The shape

- **`WorldEvent`** (Contracts) — a typed fact: `Kind`, `ActorId`,
  `ParticipantIds`, `ActionName`, `Keywords`, `ObjectIds`, `Where`, `Message`,
  `Loud`.
- **`EventDistributor`** (Services) — takes a fact, works out who could have
  noticed, hands it to each of them, keeps the ledger.
- **`IEventReaction`** (Contracts) — the seam. What noticing *does*.

`ParticipantIds` is a **list**, not a single target, because a group is the
normal case rather than the exceptional one: a conversation, a band travelling
together, and a brawl all name several people who were part of what happened.
One shape covers all of them and the distributor fans out over it without a
second event type.

### The audience tiers

```
Actor        you did it
Participant  it was aimed at you — you were part of what happened
Witness      you were near enough to notice (12m). You are owed nothing.
```

This distinction is the whole point. Being punched, seeing someone punched, and
doing the punching are three different pieces of information about the same
fact, and a reaction that cannot tell them apart will either make bystanders
bleed or leave the world stopping at the edge of the two people involved.

Two thresholds that are deliberately different: **witnesses reach the reactions
unconditionally, but reach the personal logs only when the event is loud.**
Noticing and writing it down are not the same bar — a dossier capped at thirty
entries would otherwise fill with everything anyone did nearby, and the dossier
is the one surface where noise costs the player something.

### Prose is a rendering

`Message` is carried alongside the facts rather than instead of them, because
the logs and UI have always read prose and this keeps them working. Everything
that *reasons* about an event reads the typed fields, and in particular matches
`Keywords` — never `ActionName`. Same keywords-not-names rule the weight
systems live under, one level up.

### Determinism

Publishing is synchronous, the audience is walked in roster order, and nothing
here rolls dice. A seeded run distributes events in exactly the same order every
time. The witness sweep is a linear scan over the active roster — O(n) per
event, nothing at settlement scale, and precisely the call site the spatial grid
in `../active/Decision_Appraisal_Design.md` §4 replaces when population makes it
matter. Keeping it in one method is deliberate.

Measured (`SmokeTest`, 3-entity default world, ~370 rows): 362 actor slots, 184
participant slots, **293 witness slots**. Before this, the witness number was
structurally zero.

---

## What was deliberately left open

The user's framing was *"at least where entities can realize there is a world
around them — the path past that is something else."* So the spine is built and
the reaction list ships empty. Named, in rough order of payoff:

1. **Generalise `SurfaceDesire` out of DialogService** into an `IEventReaction`
   keyed on `WorldEvent.ObjectIds`, so hearing *any* event name an object runs
   the want counterfactual — not just hearing it named in dialog. Luna's
   `ArcDef.Surfaces` is exactly this, generalised; its remarks name EARG's
   version as the thing it was generalising.
2. **Witness appraisal** — `Decision_Appraisal_Design.md` §2's *"norms are
   emergent, not stored"* has no home until events reach bystanders. It does
   now: "+rep with the boys" is the sum of witnesses each valuing what they saw
   through their own preferences. This is the natural next consumer.
3. **Begin-events.** Only settlement is announced today, so a shift is silent
   for eight beats. A quiet `EventKind.Action` on commit would let the world see
   someone clock on.
4. **Starving entities still choose Work and bail on the first beat**, wasting a
   beat each time. Correct behaviour from the commitment layer (a starving
   entity should not be at the bench); the wasted beat comes from the weight
   pipeline offering Work at all, which is P2.22's decorative critical band, not
   this feature.
5. **Nothing persists.** `Entity.Current` and the ledger are runtime-only. Both
   were designed to serialize without help — the activity is
   `(actionName, targetId, elapsed, progress)` and nothing polymorphic is ever
   written — but save/load does not exist yet (R7).

## Where each piece lives, and why

| Piece | Layer | Because |
|---|---|---|
| `Activity`, `Commitment`, `WorldEvent`, `IEventReaction` | Contracts | state and identity; trivial self-accessors at most |
| `CommitmentCalculator`, `EventDistributor`, `EntityService.Focus` | Services | the HOW — pure functions over entities, no state of their own |
| begin / advance / settle / publish | EntityARestaurantGame | the WHEN — the controller owns timing and the logs |

Nothing went into `Engine.*`. The distributor reasons about `Entity` positions
and rosters, which makes it game-aware by definition; the generic half it would
eventually want is the spatial grid, and that one *does* belong in `Engine.*`
when it arrives.
