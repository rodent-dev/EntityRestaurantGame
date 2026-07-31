# IEvent — Worked Examples

Concrete events built on the bare `IEvent` contract. The tick is the trigger —
no comparisons. Deadlines are scheduled events; durations (rare) are stored-tick
subtractions. Interrupts are signals (`Cancel` / `Stall` / `Replace`) that
transform `FireTick`. Intents are the recoverable source of truth; the live
event is always disposable.

Each example lists: **Timeline**, **what `Fire` does**, **reschedule rule**, and
**interrupt behavior**.

---

## 1. Hunger Pulse

A whole-colony "everyone gets hungrier" beat.

- **Timeline:** `SelfMaintenance` — re-derivable. If the owner (or region) goes
  dormant, drop it; lukewarm catch-up reconstructs hunger from elapsed time on wake.
- **Fire:** increments hunger on the relevant pawns.
- **Reschedule:** `FireTick + 100` (drift-free; never `now + 100`).
- **Interrupts:** rarely interrupted. If the region sleeps, it's simply dropped
  and re-derived; it carries no world-facing weight.

> Demonstrates the cheapest event: pure self-rescheduling, no render cue, absent
> from 99 of every 100 buckets.

---

## 2. Spell Channel + Impact

The "1-tick truth event + 96-tick cosmetic channel" pattern. Origin can move
(point-and-click caster), destination is fixed.

Split into two cooperating events:

**ChannelImpact** — the authoritative beat.
- **Timeline:** `ActiveDuration` — only meaningful while the caster is mid-cast.
- **Fire:** applies the actual effect (damage, status) at the fixed destination.
  Single tick. This is sim truth.
- **Reschedule:** one-shot (`null`).

**ChannelSegment** — the cosmetic re-anchor chain.
- **Timeline:** `ActiveDuration`.
- **Fire:** resamples the caster's *current* position, emits a render cue
  `(tag, currentOrigin, fixedDest, start, end)`, schedules the next segment.
- **Reschedule:** `now + step` until the channel's end tick is reached.

- **Interrupts:**
  - `Cancel` (cast interrupted, caster stunned/killed) → drop the intent; both
    events no-op. Nothing mutated mid-flight, so nothing to undo. A "fizzle" cue
    may be emitted.
  - `Stall` (briefly suspended) → shift remaining segment ticks and the impact
    tick forward by the stall duration; the cast resumes where it left off.
  - `Replace` (recast / retarget) → if conditions allow, write a new intent with
    the new target; old events no-op, new channel schedules from current tick.

> Moving origin is handled by **resampling per segment**, so every segment is a
> clean fixed-anchor lerp that can't desync. Truth lives in the single impact tick.

---

## 3. Debt Collection

A faction-held claim on the player's production (e.g. wool owed for borrowed sheep).
Models deadlines as events — no "is it overdue?" check anywhere.

**CollectionDue** — the payment beat.
- **Timeline:** `WorldObligation` — fires regardless of whether the owner is
  dormant. Dormancy must not let an entity dodge its debts.
- **Fire:** attempts to assemble the owed `IItem[]` from available inventory and
  hand it off (via the trade/transaction machinery). Records the result:
  paid / partial / missed.
  - If physical collection is used, instead of transferring directly it spawns a
    **collection task** (an entity with a location → interceptable).
- **Reschedule:** `FireTick + frequency` for a recurring agreement, until the
  terminal condition (target met / duration elapsed) → then `null`.

- **Interrupts:**
  - `Cancel` (debt forgiven / contract voided) → drop the intent.
  - `Stall` (grace period granted) → shift the next due tick forward.
  - `Replace` (renegotiated terms) → write a new intent with new amount/frequency.

**Escalation without comparisons:** rather than checking "how overdue,"
schedule consequence events — reminder at `due - 100`, default at `due + 200`,
worse penalty at `due + 400`. The *sequence of scheduled events* encodes the
escalation; the wheel remembers the deadlines so logic never asks the clock.

> On a missed payment, `Fire` emits a "defaulted by amount Y to faction Z" signal.
> It does **not** apply the consequence itself — the faction system hears it and
> decides the goodwill hit.

---

## 4. Faction Drama Roll

The periodic "should something dramatic happen" check that feeds the event system.

- **Timeline:** `WorldObligation` — the world keeps stirring whether or not the
  player is watching any particular faction.
- **Fire:** reads current goodwill across faction pairs from the relationship
  registry, rolls weighted by how bad relations have gotten; if it hits, queues a
  concrete drama event (raid, embargo, torn-loyalty incident).
- **Reschedule:** `FireTick + interval`.

- **Interrupts:** generally not interrupted; it's a standing world heartbeat.

> The roll only *selects and queues* — it doesn't resolve drama inline. The
> queued event does that on its own tick. Goodwill-lowering actions (defaults,
> raids, insults) all feed the same number this roll reads, so every hostile act
> routes through one drama pipe.

---

## 5. Torn-Loyalty Incident

Fires when a pawn's personal affinity and their faction's standing strongly
disagree (the blend reports "torn, large magnitude"). The payoff of the
relationship system.

- **Timeline:** `ActiveDuration` — it's about a specific pawn in a live situation.
- **Fire:** presents/resolves the tension. Possible outcomes rewrite registry edges:
  - refuse the faction → faction standing toward the pawn drops (own side distrusts them)
  - comply reluctantly → personal affinity decays, mood penalty
  - defect → a `member-of` edge is rewritten (pawn leaves their faction)
- **Reschedule:** one-shot.

- **Interrupts:**
  - `Cancel` (situation defused before it resolves) → drop the intent.
  - `Replace` (circumstances change — the friend dies, the war ends) → re-derive
    against the new world state, or cancel if no longer meaningful.

> Eligibility is the blend's disagreement magnitude. The resolver flags the
> tension; this event dramatizes it; its outcome writes back to the registry.
> Every system in the design participates in one pawn's bad day.

---

## 6. Region Reactivation (Catch-Up Roll)

When a dormant region wakes (player approaches, or a world event activates it),
its entities reconcile the time they spent dormant.

- **Timeline:** `WorldObligation` (it's a world-state transition).
- **Fire:** for each entity in the region:
  - **continuous state** (hunger, regen, age) → closed-form catch-up:
    evaluate the elapsed gap (`now - lastTickedAt`) by formula, once. Exact.
  - **discrete state** (did they wander, fight, get hurt) → roll a summary
    scaled by elapsed time. Plausible, not exact — acceptable because it's
    off-screen history nobody witnessed.
  - re-schedule each entity's `SelfMaintenance` events fresh; ensure its
    `WorldObligation` events are (still) in the wheel.
- **Reschedule:** one-shot per activation.

- **Interrupts:** n/a — it's an instantaneous transition.

> This is the only place a tick *subtraction* legitimately appears, and it
> measures a **gap** (duration), not an **order** (which came first) — so the
> half-range ambiguity never applies, as long as dormancy is bounded.

---

## Cross-cutting notes

- **Tick = trigger.** Every example fires by *arriving at its bucket*, never by
  comparing tick numbers. Deadlines → scheduled events. Durations → stored-tick
  subtraction (only the catch-up case).
- **Intents are truth, events are disposable.** Every interrupt is an operation
  on the intent (`Cancel` drops it, `Stall` shifts it, `Replace` rewrites it);
  the live event is rebuilt from the intent as needed.
- **`Fire` announces, systems decide.** Events that have consequences (a default,
  a drama hit) emit a signal and let the owning system apply the effect — they
  don't reach across and apply it themselves.
- **Timeline tag routes dormancy.** `SelfMaintenance` is droppable/re-derivable,
  `WorldObligation` persists through dormancy, `ActiveDuration` only matters while
  the owner is live. The same continuous-vs-discrete line shows up in dormancy,
  in event timelines, and in deadlines-vs-durations — one boundary, three views.
