# Entity Model — Implementation Handoff

**Purpose:** implement the entity-ontology decisions resolved in design. This is written for a coding agent that has the canonical source on the PC. The design was done at spec altitude against *assumed* structure — every seam below is marked either CONFIRMED (settled design intent) or **VERIFY** (check against real code before changing). Do not invent abstractions beyond what's listed; the governing rule is *dissolve, don't build* — a thing earns a new interface/component only when a system actually selects on it.

---

## 1. Goal

Move the world model off any `IEntity`-vs-`IPlace` type split and onto **capability composition**, introduce a **descriptor substrate** as the single relevance read-surface, and model the world as **two graphs** (spatial containment + aspatial ownership). Wire the place→preference→mood loop through existing Status.

No new systems. Everything here either (a) adds a capability interface a system already wants to query, (b) extends an existing operator, or (c) is plain data.

---

## 2. Resolved architecture

### 2.1 Descriptor substrate
- `IDescribable` exposes `GetDescriptors()`. Descriptors are **emitted by components, not stored centrally** — `GetDescriptors()` aggregates over the entity's components, each describing itself.
- A descriptor is an **(axis, value) pair**, e.g. `(hair.color, black)`, `(hair.texture, wavy)`, `(ideology, devout)`, `(climate, cold)`. The axis prefix is the emitting component's identity — not stored in the token.
- Rationale (do not regress): a single compound token (`Hair_black_wavy`) over-binds (only matches identical compounds, no partial credit); a bare value token (`black`) collides across axes. (axis, value) is the only shape that gives partial credit within an axis and no false matches across axes.

### 2.2 Capability interfaces (composition over inheritance)
Replace any monolithic entity type with capability interfaces, each backed by a component that holds the behavior. An interface exists **only because some system selects on it**:
- `IDescribable` — emits descriptors. Queried by the scorer / every relevance read.
- `ISpatial` — has a position. Queried by spatial systems.
- `IContainer` — contains other things. Queried by containment walks.
- `IActor` — **owns a decision loop** (an `EntityDecisionEvent` / `ActOnce`). This is the *only* capability that deliberates.
- `IRelational` — carries relationship/affinity weights.

Interface = the contract a system queries on. Component = where the shared behavior lives. Don't add `IFlammable`-style interfaces nothing queries; those are descriptors, not interfaces.

### 2.3 Two-graph world model
**Graph A — spatial containment:** `region ⊃ settlement ⊃ building`. All three are `IDescribable + ISpatial + IContainer`, differing only in **grain**, not kind. "Where you are" = union of descriptors down the containment chain (region ambient + settlement + building). Region emits coarse ambient descriptors (cold, forested, arid); finer layers add on top.

**Graph B — ownership (aspatial):** a **faction** is `IDescribable + IRelational`, **not** `ISpatial`. It does not sit in Graph A; it *points into* it via **ownership edges**. Its apparent location is derived (wherever its stuff is).

**Party** threads both: `IContainer + ISpatial`, **not** `IActor`. It doesn't deliberate. The faction's "will" is borrowed from the **leader entity** (an ordinary `IActor`) of its leader party. Faction declares war ⇒ that leader selected an aggressive action ⇒ the party follows because it contains him. No faction-level or party-level AI.

Ownership is **its own labeled edge**, not affinity — you can own something you hate or love a rival you don't control. Ownership answers "who controls this"; affinity feeds the scorer. Keep them separate. This is **not** a graph engine — it's an owner reference plus the existing spatial system.

### 2.4 Preferences
A **dumb weighted JSON list** in descriptor vocabulary: `[{axis, value, weight}, …]`. No structure beyond that. Scored against another `IDescribable`'s `GetDescriptors()` via the connection operator (§3.3). Keep the asymmetry sharp: **descriptors = "what I am"; preferences = "what I value"** — same vocabulary, opposite role. A place describes but does not value (pure `IDescribable`); an entity does both.

### 2.5 Place → preference → mood loop
Place emits descriptors → entity's preference set scores against them → resulting affinity deposits into mood as a **standing Status contribution recomputed each beat** (present = felt, absent = gone). **No decay timer** — decay would invite leave-and-revisit gaming. This lands in `StatusWeightCalculator`, not a new system.

---

## 3. Changes by seam

### 3.1 `IDescribable` + descriptor emission — **VERIFY**
- Add `IDescribable` with `GetDescriptors()`.
- Confirm components can each expose a describe method that `GetDescriptors()` aggregates. **VERIFY** the component base supports this; if components currently can't self-describe, add a minimal `IDescribe`-on-component hook rather than a central registry.
- Descriptor type: `(string axis, string value)` (or your interned equivalent).

### 3.2 Capability interfaces — **VERIFY**
- Introduce the five interfaces in §2.2 as markers over existing components.
- **VERIFY** current entity/place/component base so these compose without a parallel hierarchy. The decision loop currently lives on `EntityDecisionEvent` — `IActor` should mean "owns one of these."
- Resolve `IActor` boundary per §4: deliberation ⇒ `IActor`; non-actors may still be the *target* of scheduled wheel events.

### 3.3 Connection operator — `MatchCount` → axis-grouped overlap — **VERIFY/CHANGE**
- Current operator is flat `MatchCount` (count of shared keywords). **This is the one real algorithm change.**
- New behavior: **group both sides by axis, score agreement per axis, sum.** Must distinguish:
  - shares-axis-same-value (full credit),
  - shares-axis-different-value (partial credit — see §4 fork),
  - lacks-axis (no contribution).
- Still overlap, still cheap. **VERIFY** call sites of `MatchCount` so the signature change propagates (dialog relevance, preference read, faction alignment all route through this).

### 3.4 Preference type — new, trivial
- `Preference = List<(axis, value, weight)>`, serialized as JSON.
- Add a read: `ScorePreference(prefSet, target: IDescribable)` → runs §3.3 over `target.GetDescriptors()`.
- This same read serves all three aims: vs. another character (relationship modifier), vs. incoming speech intent (chat flavor), vs. faction ideology (alignment). One operator, three targets — do not write three readers.

### 3.5 Place-affinity into Status — `StatusWeightCalculator` — **VERIFY**
- On each beat, for an entity, walk its containment chain, union the descriptors, run `ScorePreference` against them, deposit the result as a **standing** Status contribution (recomputed, not applied-and-decaying).
- **VERIFY** `StatusWeightCalculator` is where per-beat standing contributions belong.

### 3.6 World types
- `Region`, `Settlement`, `Building`: `IDescribable + ISpatial + IContainer`, parameterized by grain. Likely one type with a grain/level field rather than three classes — **VERIFY** against how places exist today.
- `Faction`: `IDescribable + IRelational`, ownership edges, contained parties. **Not** `ISpatial`.
- `Party`: `IContainer + ISpatial`. **Not** `IActor`. Holds a leader entity reference.

---

## 4. Forks — resolved to defaults (override if you disagree)

**Fork 1 — ownership edge directionality.** DEFAULT: **bidirectional / queryable both ways** (give me everything this faction owns / give me this settlement's owner). Reason: a settlement changing hands requires re-evaluating every relation that referenced its old owner — turn one in a faction-warfare sim, not someday. The cheaper one-way `Settlement.Owner` field is the fallback only if reverse lookups prove unused.

**Fork 2 — axis overlap partial credit.** DEFAULT: **partial credit** for shares-axis-different-value (relationships and faction alignment move on a gradient). The alternative (exact-value-match only ⇒ step function) is available by zeroing the partial term. This choice sets gradient-vs-step for *all* relevance reads, so keep it one knob.

**Fork 3 — `IActor` boundary.** DEFAULT: deliberation is `IActor` (owns a decision event); **non-actor things can still be the subject of scheduled wheel events** without owning a decision loop. Lets "settlement produces goods" be a wheel event without giving settlements a will.

---

## 5. Out of scope (do not build now)
- Applied amplifiers with **scheduled decay** — kept only for discrete moments (potion/punch), not ambient place-affinity. Not needed for this pass.
- Chained-decay (`nextAmplifier`) — deferred; one-field add later if runtime asks.
- Stats-as-descriptors / stats-as-amplifiers — only if a relevance read actually wants stats as standing pressure. Not yet.
- Packaging `Engine.Ticks/Items/Spatial` as standalone libraries — later, near-zero refactor.

---

## 6. Verification checklist before committing
1. `GetDescriptors()` aggregates from components, not a central store.
2. `MatchCount` replaced by axis-grouped overlap at every call site.
3. No new `IEntity`/`IPlace` hierarchy introduced — only capability interfaces over existing components.
4. Faction is not `ISpatial`; party is not `IActor`.
5. Place-affinity is recomputed standing Status, no decay timer.
6. Ownership is a labeled edge distinct from affinity weight.
