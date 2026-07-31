# Entity Model — Resolved Design

**Status:** design resolved (2026-06-24), supersedes the open VERIFY/fork items in
`EntityModel_Implementation.md`. This is the *what and why*; the *how and in what order*
lives in `EntityModel_Plan.md`.

---

## 0. The core insight

Everything in this rework rides **one operator**: an *axis-grouped descriptor overlap*.
`IDescribable` stops being cosmetic ("brown straight hair") and becomes the **substrate the
whole weight system reads**. The same operator is used four ways:

1. **Descriptors** — what a thing (and its parts) *is*, aggregated recursively.
2. **Behaviors** — an inner pressure (`Affect` payload) matching an action's descriptors.
3. **Moods** — a transient feeling amplifying behaviors/actions that share its descriptors,
   plus stat effects.
4. **Outcome grading** — how well a target matches an action's *expected* descriptors decides
   success vs. flavored failure.

Author qualities as data; the operator turns them into pressure. No `if (name == …)` branches.
This is design pillar #5 (keyword/descriptor-driven, no hardcoded action-name logic) extended
from actions to descriptions, feelings, and outcomes.

---

## 1. Descriptor substrate (`IDescribable`)

- **`GetDescriptors()` is a recursive tree walk.** A node emits its own descriptors and folds in
  those of any owned `IDescribable` children. The entity emits identity-level descriptors and
  owns parts (Head/Arms/Legs/Torso); `EntityPhysique` grows from `{Weight, Height}` into a parts
  container. The *same* mechanism describes non-entity objects later — an object is just an
  `IDescribable` that may own `IDescribable` parts.
- **A descriptor is `(axis, value)` carrying an `Affect`** (signed scalar). Example:
  `hair → (color, brown), (texture, straight)`; a behavior emits `(disposition, aggressive)`
  with `Affect = +N`. The `Affect` is the magnitude the operator multiplies by — it is *not* a
  separate system; `BehaviorModule.Affect * Scalar` (already `EntityService.cs:79`) is exactly
  this payload, preserved as-is.
- **Axes are path-qualified by slot** on the way up the tree (`head.skin.color` vs `arm.skin.color`)
  so partial-credit-within-axis stays meaningful — "his face is scarred" and "his arm is scarred"
  score separately.

## 2. The connection operator (axis-grouped overlap)

Replaces the flat `MatchCount` (`BehaviorModule.cs:26`, only caller `EntityService.cs:77`).

- **Group both sides by axis, score agreement per axis, sum, weight by `Affect`.** Distinguishes:
  shares-axis-same-value (full credit), shares-axis-different-value (**partial credit** — the
  resolved default; relationships and alignment move on a gradient), lacks-axis (no contribution).
- Still overlap, still cheap. Returns a number. (`Affect` is a signed scalar today; multi-dimensional
  values are a possible later extension and the one remaining cosmetic knob.)

## 3. Capability interfaces (composition, no hierarchy)

Markers over existing components — an interface exists **only because a system selects on it**.
No `IEntity`/`IPlace` split is introduced (none exists today to dismantle).

| Interface | Means | Backed by (today) |
|-----------|-------|-------------------|
| `IDescribable` | emits descriptors | Entity + its parts/components |
| `ISpatial` | has a position | `Entity.Position`; a place's `Location` |
| `IContainer` | contains other things | `Entity.Inventory`; a place's contents |
| `IActor` | **produces action candidates + gets a deliberation beat** | scheduled `EntityDecisionEvent` |
| `IRelational` | carries affinity weights | `Entity.EntityRelationships` |

`IActor` does **not** carry the deliberation logic (that stays in service/controller per the
Contracts/Services/Controllers rule). It is the marker that the controller runs the *weighted draw*
for this thing. Settlements "producing goods" are a plain `IEvent` subject, never `IActor`.

## 4. Two-graph world

- **Graph A — spatial containment:** `region ⊃ settlement ⊃ building`, all the *same* enriched place
  type differing by **grain**, all `IDescribable + ISpatial + IContainer`. "Where you are" = union of
  descriptors down the containment chain. `LocationRegistry.FindAllLocations` already returns
  smallest-first = a derived containment chain.
- **Engine generic, game enriches via composition:** `Engine.Spatial.Location` stays pure geometry
  (references nothing). A `Game`-side place **owns** a `Location` (HAS-A, not inherits) and adds
  `IDescribable + grain + containment`. One general place object grows into region/settlement/building.
- **Graph B — ownership (aspatial):** a `Faction` is `IDescribable + IRelational`, **not** `ISpatial`;
  it points into Graph A via labeled **ownership edges** (distinct from affinity). A `Party` is
  `IContainer + ISpatial`, **not** `IActor` — it follows the leader entity it contains (the lone
  actor). *(Greenfield; sequenced after the core loop — see plan.)*

## 5. Moods — a decaying bag, not a meter

- **The single 0–100 `EntityNeeds.Mood` int is removed.** Replaced by a **bag of named moods**
  (`elated`, `miserable`, `dread`), each a generic describable thing carrying its own descriptors
  (`dread → (disposition, timid),(disposition, fearful)`), a magnitude, and stat effects (dread
  debuffs combat stats). Moods **coexist and blend** — nothing overwrites.
- **A mood amplifies behaviors/actions sharing its descriptors via the same connection operator.**
  "Dread makes you timid and weaker" is authored as data, not coded.
- **A mood touches both substrates:** it is an `IStatus` when read for weighting/stats, and its
  magnitude is driven by an `IEvent` **decay pulse** (sibling of `HungerPulseEvent`) plus its
  source's per-beat **top-up**. A region/status mood is re-applied each beat while you're in the
  condition (top-up > decay → it holds while present; leave → it drains and fades). A one-shot event
  (romance) deposits once and decays from peak.
- This **overrides the spec's "no decay timer"** — safe because place moods are continuous top-ups,
  not entry bursts, so the leave-and-revisit exploit the spec feared can't be farmed.
- **Place-affinity** = `preference × place-descriptors → a mood`, deposited this way.

## 6. Preferences

A dumb weighted JSON list in descriptor vocabulary: `[{axis, value, weight}, …]`. Scored against any
`IDescribable.GetDescriptors()` via the connection operator. **Descriptors = "what I am";
preferences = "what I value"** — same vocabulary, opposite role. A place describes but does not value
(pure `IDescribable`); an entity does both. One read (`ScorePreference`) serves character-vs-character,
character-vs-place, and character-vs-faction alignment — three targets, one operator.

## 7. Evaluation lives OUTSIDE the entity (`IStatus` pipeline)

The weighing — influence + mood + location + relationships + behaviors → flat numbers — does **not**
belong on the entity. Today it is a hardcoded sequence in `ActOnce`
(`ApplyBehaviorWeights; ApplyStatusWeights; ApplyInfluence`). Replace with a **registry of
contributors**:

```csharp
interface IStatus
{
    void Contribute(EvaluationContext ctx, IReadOnlyList<ActionCandidate> candidates);
}
```

- Each contributor *reads* actor + world (via `EvaluationContext`) and *appends*
  `ActionWeight { Weight, Source }` — the established flat-number model, unchanged.
- Behavior-match, mood, relationships, place-affinity, player-influence each become **one** `IStatus`.
  `StatusWeightCalculator` demotes from "home of all status logic" to a single contributor.
- `ActOnce` iterates the registry instead of naming passes → new pressure = new `IStatus` + register,
  zero edits to `ActOnce`.
- `IStatus` is **state-pulled on read, never queued** (unlike `IEvent`), so it cannot bottleneck the
  wheel. This is the deliberate complement to the tick substrate:

| | `IEvent` (exists) | `IStatus` (new) |
|---|---|---|
| Driven by | time — scheduled to a tick | state — evaluated on read |
| Lifecycle | discrete, fires once | standing, recomputed each read |
| Home | the wheel (a queue) | a contributor registry (no queue) |
| Good for | decision beats, hunger/mood **decay** | the weight pipeline, place-affinity |

## 8. Action ↔ target

- **Action candidates** = `item × action` combos = *what I can do*, intrinsic to the `IActor`
  (from `ActionModule` × `Inventory`). **Actable targets** = who/what is in range = controller-supplied.
  A real `ActionCandidate` is the filtered cross-product.
- **Target eligibility is a per-action capability query**, not one fat `IInteractable`. Each action
  selects the narrow capability it needs (`Attack`→has-health, `Speak`→a mind, `Consume`→an item,
  `Enter`→a place). This is the existing `IAction.CanTarget(user, target)` gate, generalized from
  `Entity target` to a capability/`IDescribable` target so **objects become first-class targets**.

## 9. Outcome grading + deny-list gating

The attempt is broadly allowed; the **effect** is what's gated, with flavored failure rather than
silent exclusion. "Can this effect land here?" resolves in two layers — only the second costs storage:

- **Layer 1 — derived from identity (free, no storage):** the degree of descriptor match between the
  action's expected-target (`Consume → (edible)`) and the actual target grades the outcome. Metal lacks
  `edible` → effect denied → *"the character struggles to eat the metal!"*. Covers nearly all
  "doesn't make sense" cases implicitly via the connection operator.
- **Layer 2 — explicit anti-actions (stored only when attached):** a small deny list for exceptions
  descriptors don't imply — god mode, fire immunity. **Keyed by descriptor, not action-name**
  (`immune to (damage,*)` or `(damage,fire)`), so new matching actions are auto-denied. Only the few
  entities that need it pay the tracking.
- **Two voices:** derived mismatch → "struggles" (the *actor* fails); explicit anti-action →
  "bounced off" (the *target* negates).
- **Default:** explicit denial gates **outcome** (stays selectable, narrates the bounce), keeping
  flavorful long-shots alive; candidacy-denial is reserved for options you truly want hidden.

---

## 10. Preserved / out of scope

**Preserved:** weighted-random draw (pillar #3), flat `ActionWeight` model, `Affect*Scalar`,
Contracts/Services/Controllers layering, `Engine.*` referencing nothing, the `IEvent` wheel.

**Out of scope this pass:** faction/party warfare beyond the data scaffold; chained-decay
(`nextAmplifier`); multi-dimensional `Affect`; packaging engines as standalone libraries; a real
renderer.

---

## 11. Map to current files

| Concern | Today | Becomes |
|---------|-------|---------|
| Connection op | `BehaviorModule.MatchCount` (flat) | axis-grouped operator (Game.Contracts/Services) |
| Behavior keywords | `BehaviorModule.Keywords : HashSet<string>` | descriptors `(axis,value,affect)` |
| Status weights | `StatusWeightCalculator` (static, hardcoded tables) | one `IStatus` contributor |
| Weight passes | `ActOnce` names `ApplyBehaviorWeights/...` | `ActOnce` iterates `IStatus` registry |
| Mood | `EntityNeeds.Mood : int` | mood bag + decay pulse + `IStatus` |
| Parts | `EntityPhysique {Weight,Height}` | recursive `IDescribable` parts container |
| Place | `Engine.Spatial.Location` (geometry only) | game place HAS-A `Location` + descriptors/grain/containment |
| Target gate | `IAction.CanTarget(user, Entity)` | generalized to capability/`IDescribable` target |
| Decay precedent | `HungerPulseEvent` | mood-decay pulse mirrors it |
