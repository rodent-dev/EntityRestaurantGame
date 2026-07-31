# Decision & Appraisal — Deliberation, Emissions & the Spatial Grid

Design conversation captured 2026-07-21. **Design direction, not yet built.**
Companion to `SimDepth_Purpose_Plan.md` (which made Work a candidate) and
`RECOMMENDATIONS.md` P2.19 (status-band rethink). Nothing here is implemented;
this is the model we talked ourselves into and the open threads that remain.

## 0. The problem this solves

Entities today are **pure instinct**. Every weight source in the pipeline —
behaviors, relationship, status, proximity (`EntityService`,
`StatusWeightCalculator`) — is *dispositional*: it answers *"do I feel like
it?"* Not one answers *"is it worth it?"* A purely dispositional decider is,
by definition, a non-thinking animal. That's the mechanical root of the
symptoms:

- **Too aggressive / unprovoked.** `SelectWeighted` floors every candidate at
  `Math.Max(1, weight)`, so a motiveless Attack against any target keeps a
  floor chance every beat. Aggression leaks through the floor rather than
  emerging from a motive.
- **No reason to be aggressive.** Temperament is a *constant* additive bias
  (`Affect * Scalar * matchCount`), applied every beat toward everyone — a
  disposition with no trigger, instead of a reactive response function.
- **Lack of order.** Each beat is a fresh memoryless roll; only the
  relationship integer persists, and combat souring relationships is an
  *asymmetric* loop (hostility compounds, warmth doesn't) that drifts the
  population hostile.
- **Actions feel narrow.** In the calm middle band nothing *pulls* toward the
  peaceful verbs, so floor-lifted Attack competes evenly with Idle. The
  missing thing is a compelling default pursuit ("work, buy, enjoy").

The fix is the missing half of the mind: an **instrumental** appraisal layer.
Note: this is the *same* missing layer as "what is the player *for*" — NPC
naturalness and player role are both "no representation of what an agent is
for." Player role is parked but sits directly downstream of this.

## 1. The decision pipeline

Cost rule that shapes everything: **appraise the one action instinct already
chose — never score the whole candidate set.** Weighing one action's result is
cheap; weighing all candidates is not.

1. **Instinct proposes ONE action** — the existing cheap dispositional
   weighted roll picks a single candidate.
2. **Appraisal judges that one action** and produces a **3-way weighted roll:
   commit / veto / flip.** All value dimensions bundle into these three
   weights; then roll.
   - **commit** — do it. High when the emission is net-favorable to what the
     mind values.
   - **veto** — don't. Resolves to idle by default (same exit as "nothing is
     worth doing"). *Open: veto → idle, or veto → one cheap fallback?*
   - **flip** — right drive, wrong manner: **polarity inversion on the same
     target/drive** (insult→compliment, shove→steady). Kept to valence
     inversion so it stays O(1) and is not a disguised reroll.
3. **The 1% snap is free.** Residual *commit* weight winning despite a bad
   appraisal is the rare outburst — no pent-up accumulator needed. The causal
   "provoked → more likely" part rides the persistent mood/relationship state
   already feeding the instinct roll; only "suppression itself charges you up"
   would need a dedicated pressure variable, and we're skipping that for now.
4. **"Does instinct take over?" = the mixing gain** between the instinct family
   and the appraisal family of weights, per mind. Low intelligence / high
   impulsiveness → appraisal downscaled → instinct dominates (the beast
   punches the customer because the "−resources, −standing" appraisal barely
   registers). High intelligence → appraisal swamps instinct. Stupidity is
   literally the appraisal multiplied toward zero. This is the `Wisdom =
   beast-affinity` law as a single coefficient.

## 2. Emissions — consequence without lookahead

An action (amended by context) **emits a tagged result signature**:
`resources−, health−, relation(X)+, faction(Y)−`, etc. Appraisal dots that
signature against the mind's values. **No simulation, no lookahead.**

- **Same currency for mechanical and abstract-social tags.** `health−` from a
  `DamageEffect` and `angers-faction` as a declared social tag travel
  identically, so the appraiser is one uniform function, not a pile of cases.
- **Authorship division (the whole ballgame): the world declares what
  happens; the mind decides if it's worth it. Neither hardcodes the
  decision.** The greedy NPC's "don't punch the customer" is written *nowhere*
  — it emerges from a generic projection ("punch → −relation, −standing")
  meeting the greedy mind's values ("I value the trade-relationship at W").
- **Norms are emergent, not stored.** "+rep with the boys" is not a rule; it's
  the sum of witnesses each valuing what they saw through *their* preferences.
  A bandit camp rewards cruelty and a temple punishes it with zero authored
  norms — you authored *what minds value*, not *what acts are worth*. This is
  the natural home of the roadmap's "action result propagation" item.
- **No lookahead — price assets as present value.** Don't simulate "punch →
  they won't trade → lost future income." Value the *relationship itself* as
  an asset held now, so damaging it registers as a present loss. Unbounded
  planning collapses into static valuation ("burning a bridge feels bad now").
- **Snowball rides persistent stocks.** An emission that lands on a *stock*
  axis (relationship, standing, faction rep) persists and recolors every
  future roll — you never predicted the snowball, you let a damaged asset ride
  forward. Flow axes (a bite of food) don't snowball; stock axes do.

## 3. Value axes & IMotive

**Proposed axes (the currency worth is denominated in):** need-satisfaction,
standing/reputation, relationship-assets, resources/means, role-fulfillment.
Small, general, all measurable by the projection. Split into **stock** axes
(relationships, standing — valued as present stock so future value folds into
one step) vs **flow** axes (immediate delta). *Axis list not yet locked.*

**IMotive** — the classification for *what pieces of the world weigh into this
person's decision*. Verdict: worth it, **as a value declaration, never a
decision function.** An IMotive is `(value axis + weight + inclination)` — a
motive is intrinsically a value+inclination pair, which is why the same
interface fits behaviors, roles, and statuses:

- greedy **behavior** → values resources, inclined to acquire
- baker **role** → values income, inclined to cook
- famished **status** → values need-satisfaction, inclined to consume

It feeds *both* families of the roll: the inclination adds an **instinct**
weight, the value adds an **appraisal** weight (it prices the projected Δ on
its axis). Behaviors are the natural starting point — `behavior.Scalar`
already *is* the value weight.

**The one line to hold:** an IMotive must never contain situational logic. The
moment a `ShopkeeperMotive` has `if (target.HasMoney)…`, the narrowness has
just moved house into the IMotive implementations. IMotive = declaration; the
projector = generic; the two never touch.

**Cost:** the interface is cheap (gather active IMotives, cache until a source
changes). The expense is the projection, bounded three ways: only project the
chosen action (§1 cost rule), cap witness reactions at one hop, and the
intelligence gain lets beasts skip it entirely.

**Pride/fear = the transient lens.** Pride and fear are transient
re-weightings of the axes *at appraisal time* — cheap scalars over the value
profile sourced from mood/recent events/temperament. Fear up-weights
threat/self-preservation (→ `angers-faction` reads as catastrophe → veto);
pride up-weights standing (→ backing down is the real cost → commit into a bad
material trade). Same emission, different commit/veto/flip weights. This is
where "how they come out of the encounter" lives.

## 4. Spatial grid & performance (design settled)

Goal correction: entities should **not** sleep — every active entity reasons
every tick. We only cheapen the *inputs* to that reasoning. ("Content to idle"
is a *decision outcome* via veto, not a compute-skipping trick — keep the two
separate.)

- **Engine spatial grid** (game-agnostic → belongs in `Engine.*`; the
  candidate-building that consumes it stays in Services).
- **Chunk edge ≥ max interaction reach.** Then the **3×3×3 block** centered on
  an entity's chunk provably contains every reachable entity — this is the
  "set of boxes, not one chunk" that stops border-blindness, and it's
  *provable*, not fiddly. Size chunk ≈ reach to minimize wasted volume.
- **Cache the neighborhood union per chunk, not per entity.** Entities in the
  same chunk share the identical union → compute it once per occupied chunk,
  not once per entity. A chunk transition becomes "stop reading union(A),
  start reading union(B)" — B's union already exists. The "recompute fully on
  chunk change" cliff evaporates; it only exists if you cache per-entity.
- **What's shared vs per-entity:**
  - *Shared, per-chunk:* occupancy + neighborhood union, maintained by
    enter/leave deltas. Nearly free. Kills the O(n²) "who can I see."
  - *Per-entity, cheap:* distance-filter the union (a superset) down to true
    reach.
  - *Per-entity, the real cost:* weighting each surviving candidate.
- **Weights: recompute, do NOT diff.** The grid caps the candidate list to
  *local crowd density* (not total population); recomputing a few sums over a
  small list every tick is cheap (low-millions of ops/tick at 1k entities).
  Weights are "flexible" *because* they depend on many drifting inputs — which
  is exactly what makes them the wrong thing to cache. Diffing buys ~2–3× at a
  nasty cache-invalidation cost. Only reach for pair-scoped dirty-flags if
  profiling ever shows weighting is hot (it won't).
- **All active entities compute every tick** — no per-entity scheduling. This
  is affordable *because* the grid made per-entity cost low.
- **Dormancy, not the grid, handles the inactive thousands** (roadmap item).
  The grid holds only the live set; dormancy simulates off-screen masses at a
  coarser grain and *promotes* one into the grid when it goes active. Two
  tools, two scales — don't stretch the grid over dormant entities.

Settled shape: **Engine grid (chunk ≥ reach, per-chunk cached union) → shared
membership → per-entity distance filter → per-entity weight recompute over a
small list → instinct picks one → commit/veto/flip.**

## 5. Open threads

1. **Emission-signature authorship** — where do tags live and how does context
   amend them *without `if` soup*? Leaning: a base signature on the action,
   with the world *adding* context tags as **data on the target/faction**
   ("harming me emits `angers-faction:high`"), never as branching logic. This
   boundary is the thing that keeps it non-narrow.
2. **Lock the value axis list** (§3) — every IMotive is defined in terms of
   it; it's effectively the character sheet of the mind.
3. **Drive-vs-value split** inside IMotive — how cleanly to separate the
   inclination half from the valuation half.
4. **`veto` resolution** — straight to idle (preferred: unifies with the
   do-nothing floor) vs one cheap fallback.
5. Player role (parked) plugs in here: **influence = nudging appraisal, never
   commanding the action**; world controls (base upgrades, diplomacy) sit at a
   separate altitude above this.
