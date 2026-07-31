# Entity Model — Implementation Plan

Phased, dependency-ordered. Each phase compiles, keeps the game runnable, and is checkable via
`tools/SmokeTest`. Resolves the seams in `EntityModel_Design.md`. Layering follows
Contracts (identity) / Services (how) / Controllers (when); `Engine.*` references nothing.

**Sequencing principle:** build the *substrate and operator* first as a behavior-preserving
refactor (the world plays identically), then add new pressures on top. Nothing user-visible should
change until Phase 3.

---

## Phase 0 — Descriptor substrate + connection operator *(foundation, behavior-preserving)*

Goal: introduce descriptors and the axis-grouped operator, and migrate behaviors onto them with the
*same* numeric result the flat `MatchCount` produced. No new pressure yet.

1. **`Game.Contracts/Descriptor.cs`** — `readonly record struct Descriptor(string Axis, string Value, int Affect = 0)`.
   Consider interning axis/value strings later; not now.
2. **`Game.Contracts/IDescribable.cs`** — `IEnumerable<Descriptor> GetDescriptors()`. Default
   aggregation helper that folds owned `IDescribable` children with **slot path-qualification** of axes.
3. **`Game.Services/DescriptorMatch.cs`** (or a method on the operator's home) — the axis-grouped
   overlap: group both sides by axis, full credit on same-value, **partial credit** on different-value,
   zero when an axis is absent; multiply contributions by `Affect`. Returns `int`.
4. **Migrate `BehaviorModule`** — `Keywords : HashSet<string>` → `IEnumerable<Descriptor>`
   (`GetDescriptors()`), preserving `Affect`/`Scalar`. Provide a temporary shim so each existing
   keyword maps to a descriptor on a default axis, reproducing today's `Affect * Scalar * matchCount`.
5. **Update the one real call site** `EntityService.cs:77` to call the new operator.
6. **Action descriptors** — give `IAction` a descriptor view of its `Keywords` (the action side the
   operator scores against). Keep `Keywords` as the source initially to avoid a big-bang rewrite.

**Verify:** SmokeTest decision distributions are statistically unchanged vs. baseline (capture a
seeded run before/after). `MatchCount` has no remaining callers.

---

## Phase 1 — Capability interfaces *(markers, no behavior change)*

7. Add `IDescribable, ISpatial, IContainer, IActor, IRelational` (`Game.Contracts`).
8. Implement them on `Entity` over existing members (`Position`→`ISpatial`, `Inventory`→`IContainer`,
   `EntityRelationships`→`IRelational`, components→`IDescribable`). `IActor` is a marker only;
   deliberation stays in service/controller.
9. **Verify:** no parallel hierarchy, no `IEntity`/`IPlace` type; everything still compiles and runs
   identically.

---

## Phase 2 — `IStatus` evaluation pipeline *(refactor, behavior-preserving)*

10. **`Game.Contracts/IStatus.cs`** + **`EvaluationContext`** (carries actor, world reads, influence,
    rosters — read-only; contributors own no state).
11. Wrap each existing pass as an `IStatus`: `BehaviorStatus`, `RelationshipStatus`,
    `StatusBandStatus` (today's `StatusWeightCalculator`), `InfluenceStatus`. Each appends
    `ActionWeight` with its existing `ActionWeightSource`.
12. **`EntityController.ActOnce`** iterates a registered `IReadOnlyList<IStatus>` instead of naming
    `ApplyBehaviorWeights/ApplyStatusWeights/ApplyInfluence`. Registry built at composition root.
13. **Verify:** seeded SmokeTest output identical to Phase 1. Adding a no-op `IStatus` requires zero
    `ActOnce` edits (demonstrates the seam).

---

## Phase 3 — Mood bag + decay *(first user-visible change)*

14. **`Game.Contracts/Mood.cs`** — a named mood: descriptors (`GetDescriptors()`), `Magnitude`,
    optional stat effects. **`MoodBag`** on the entity replacing `EntityNeeds.Mood : int`.
15. **`MoodStatus : IStatus`** — for each mood, run the operator against each candidate's action
    descriptors; append amplification weights. Apply stat effects where stats are read.
16. **`MoodDecayPulse : IEvent`** (Simulation) mirroring `HungerPulseEvent` — each tick decays every
    mood's magnitude; drop at/below zero.
17. **Source top-up** — standing sources (place/status) re-apply their mood each beat; one-shot sources
    (events like romance) deposit once. Existing mood-driven code (giddy/deficit bands in
    `StatusBandStatus`, influence resistance via `MoodRatio`) reads a **derived overall valence** over
    the bag so it keeps working.
18. **Verify:** romance → elated that fades over ticks; entering a grim place → miserable that holds
    while present and drains on leaving; two moods coexist and blend. SmokeTest scenario added.

---

## Phase 4 — Places: enrich `Location`, place-affinity, preferences

19. **`Game.Contracts/Place.cs`** (or similarly named) — HAS-A `Engine.Spatial.Location`, implements
    `IDescribable + ISpatial + IContainer`, carries a **grain** field (region/settlement/building) and
    containment. Engine `Location` untouched.
20. **Containment-chain descriptors** — union descriptors down the chain via
    `LocationRegistry.FindAllLocations` (already smallest-first).
21. **`Game.Contracts/Preference.cs`** — `[{axis, value, weight}]`, JSON-serializable.
    **`ScorePreference(prefSet, target: IDescribable)`** runs the operator over `target.GetDescriptors()`.
22. **`PlaceAffinityStatus`** (or a per-beat hook) — score the actor's preferences against the place
    chain, deposit the result as a standing **mood** (Phase 3 mechanism), not a direct candidate weight.
    Add `ActionWeightSource.Location` for any direct contributions.
23. **Verify:** an entity that values "warm/forested" gains a positive mood in a matching region; the
    mood drains when it leaves.

> *Graph B (Faction/Party/ownership edges) is greenfield and deferred to a follow-up milestone — the
> core place→preference→mood loop above does not depend on it. When built: `Faction` is
> `IDescribable + IRelational` (not `ISpatial`) with labeled ownership edges; `Party` is
> `IContainer + ISpatial` (not `IActor`) holding a leader-entity reference.*

---

## Phase 5 — Targets & objects

24. Generalize `IAction.CanTarget(user, target)` and `ActionCandidate.Target` from `Entity` to a
    capability/`IDescribable` target type. Each action selects the narrow capability it needs
    (`Attack`→has-health, `Consume`→item, `Speak`→a mind), replacing any broad "interactable" notion.
25. **Objects as `IDescribable` targets in a room** — minimal object type (an `IDescribable`, possibly
    `ISpatial`) so a lemon/metal can be a target. `BuildCandidates` includes in-range objects, not just
    `activeEntities`.
26. **Action expected-target descriptors** — each action declares the descriptors a target should match
    for full effect (consumed by Phase 6).
27. **Verify:** an entity can have a low-weight `Speak`-at-object or `Consume`-object candidate; weird
    long-shots appear in the candidate set without being scripted.

---

## Phase 6 — Outcome grading + deny-list gating

28. **Outcome grading** — at `Execute`, grade effect by descriptor match between the action's
    expected-target and the actual target. Full match → full effect; mismatch → reduced/none + flavored
    "struggles" narration.
29. **Layer 2 deny-list** — optional `AntiActions` on a target, **keyed by descriptor**
    (`immune to (damage,*)`). Consulted before effect; on match, negate with "bounced off" narration.
    Default gates **outcome** (selectable, narrated), not candidacy.
30. **Narration** — route both voices through the existing announce path
    (`AnnounceDecision`/`Announce`): "struggles" (actor failure) vs "bounced off" (target negation).
31. **Verify:** eat-lemon nourishes; eat-metal narrates a struggle with no nourishment; an entity with
    `immune to (damage,*)` reports attacks bouncing off.

---

## Cross-cutting checks (run after each phase)

- `Engine.*` still references nothing; no logic leaked onto contract objects.
- UI talks only to the controller; no service reached past it.
- Seeded SmokeTest baseline captured at Phase 0 and compared through Phase 2 (must be identical), then
  re-baselined as intended behavior changes land (Phases 3+).
- New pressure types are added as data + an `IStatus`/descriptor entry, never as an `ActOnce` branch.

## Suggested commit boundaries

One commit per phase (Phases 0–2 are pure refactors and should land first and separately, so any
behavior change from Phase 3 onward is isolated and bisectable).
