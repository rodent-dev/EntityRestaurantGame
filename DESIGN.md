# Entity Influence Engine — Systems Design Document

> **Purpose.** This is a reference map of every system that exists today, how they
> fit together, and the invariants each one assumes. It is written for designing
> *new* features against — when you add a system, read the section it touches
> first so you extend the seams instead of cutting across them.
>
> Companion docs: `ARCHITECTURE.md` (layering rules) and `specs/archive/docs-6-11/`
> (original tick/event/world/speech design notes). Last synced to code: 2026-06-17.

---

## 1. The One-Paragraph Premise

You are an **influencer, not a controller**. Entities live in a restaurant world
and make their own weighted decisions every few ticks. You nudge *probabilities*
through a favored pawn; the world keeps turning whether you watch or not. Every
action is carried by an **item** (items are intent), and every decision is scored
by **keyword overlap** between an entity's traits, its current condition, and the
semantic tags of each action it could take. Nothing is hardcoded by action name —
new content participates in the math just by declaring the right keywords.

**Five design pillars** (do not violate these when adding features):

1. **Influence, not control** — the player biases weights; entities still choose.
2. **Modularity** — behaviors, stats, actions, items are attachable components.
3. **Determinism with texture** — weighted *random draw*, never argmax.
4. **Items as verbs** — most actions require an item to carry them.
5. **Keyword-driven matching** — declare tags; never branch on action names in
   weight code.

---

## 2. Architecture at a Glance

Layering follows an API-style philosophy: **Contracts** hold identities,
**Services** know *how*, **Controllers** decide *when*. The compiler enforces the
dependency direction via project references.

```
src/
├── Engine.Ticks/     GENERIC  Clock, Wheel, SimLoop, IEvent          (refs nothing)
├── Engine.Items/     GENERIC  IItem<TActor>, EffectRegistry, factory  (refs nothing)
├── Engine.Spatial/   GENERIC  Location (AABB), LocationRegistry       (refs nothing)
│
├── Game.Contracts/   IDENTITY Entity, Needs, Behavior, IAction,       (refs Engine.Items)
│                              ActionCandidate, Influence, GameSignal
├── Game.Services/    HOW      EntityService, StatusWeightCalculator,   (refs Contracts + Engine)
│                              standard Actions/, item Effects/
└── EntityARestaurantGame/  THE GAME  Program (composition root),       (refs everything)
                            EntityController (WHEN), Simulation/, UI/
tools/SmokeTest/   headless harness driving the controller without the UI
data/items/        item JSON, copied next to the executable at build
```

**Hard rules** (from `ARCHITECTURE.md`, restated because they shape every feature):

- `Engine.*` reference **nothing** — they must stay reusable in any project. No
  game concept (Entity, hunger, mood) may leak in. `Engine.Items` is generic over
  `TActor`; it routes the actor to effects but never inspects it.
- `Game.Contracts` carries **no logic** beyond trivial self-mutators
  (`TakeDamage`, `Feed`, `AdjustMood`). Identities, not behavior.
- `Game.Services` knows **how** to do everything to an entity and holds **no game
  state** of its own (only an RNG and world geometry).
- The game exe is the only place that owns state, time, and rendering.
- The UI talks **only** to the controller — never reaches past it into services.

When you add a feature, slice it the same way from the start: identity → contracts,
how-to → services, when/orchestration → controller. Reusable machinery with no game
concept in it → a new `Engine.*` library.

---

## 3. Time & Scheduling — `Engine.Ticks`

The substrate everything else rides on. There is **no per-frame polling and no
absolute time**; behavior happens because an event *arrives at its bucket*.

### 3.1 Clock
A `ushort` tick counter that **wraps on overflow by design** (65535 → 0 is free).
Total elapsed time is never stored. "Is target due relative to now?" uses
wrap-aware signed arithmetic: `(short)(target - now) <= 0`.

> **HARD INVARIANT:** nothing may be scheduled more than `32767` ticks ahead, or
> "future" and "past" become ambiguous. Re-arm long waits in hops.

### 3.2 Wheel
A flat **65536-slot timer wheel** (one slot per `ushort` value). Advancing is
O(1): step the index, drain that one bucket. Idle ticks cost nothing.

- `Schedule(e, fireTick)` / `ScheduleIn(e, ticksAhead)` — the horizon guard throws
  if you exceed `MaxHorizon`.
- `ProcessCurrent()` drains the current bucket, fires non-cancelled events, and
  reschedules any that return a next tick. Returns the count that actually fired.
- **Same-tick tie-break is insertion order** (deterministic). Events firing now may
  schedule into *future* buckets but cannot extend the bucket being drained.

### 3.3 SimLoop
An accumulator: real seconds in, fixed sim ticks out. `Pump(realDeltaSeconds)`
runs as many whole ticks as the elapsed time buys and returns how many advanced.
**Pause = stop feeding the accumulator** ("unticking"). Speed = scale the feed.
The sim only ever advances in whole, uniform ticks.

### 3.4 IEvent — the only tickable
Events *are* the tickables. Each has `FireTick`, a `Cancelled` flag, an
`EventTimeline` tag, and `Fire(clock, wheel)` which returns the next absolute fire
tick (or `null` for one-shot) and may enqueue follow-ups.

- **Lazy cancellation** is the universal interrupt: a cancelled event still reaches
  its bucket but no-ops on `Fire`. Cheapest possible interrupt — nothing has
  mutated yet, so there is nothing to undo. Used for entity death/inactivation.
- **Drift-free rescheduling:** events return `FireTick + interval`, anchored on
  the scheduled tick, never on `now`.
- `EventTimeline` (`SelfMaintenance` / `WorldObligation` / `ActiveDuration`) tags
  how an event relates to **dormancy**. **Not yet consumed** by the wheel — it is
  intent for a future dormancy layer.

> **Designing against this:** any recurring behavior should be an `IEvent`, not a
> loop that checks the clock. Drop droppable work (`SelfMaintenance`) and persist
> world obligations. Long delays re-arm in hops to respect the horizon cap.

---

## 4. Items — `Engine.Items`

Items are the **verbs** of the game: most actions can't happen without one. The
engine is fully generic and game-agnostic.

- **`IItem<TActor>`** — identity (`ItemId`, `ItemName`, `Description`, `Weight`),
  `UsesRemaining` (`-1` = infinite, `0` = depleted), and the two verbs the action
  system calls: `CanEnableAction(name, user, target)` and `Apply(name, user, target)`.
- **`ItemDefinition`** — the JSON DTO. Unknown JSON properties are ignored, so the
  game can add fields (e.g. `speakEffectModifier`) without touching the engine.
  Each item declares `enablesActions` and a list of `effects`, where each effect
  has a `type` (keyword), a `trigger` (the action name that fires it), and a
  `parameters` bag.
- **`EffectRegistry<TActor>`** — maps effect-type strings → `IActionEffect<TActor>`
  implementations. Must be populated before items load.
- **`IActionEffect<TActor>`** — a stateless, shareable unit of behavior:
  `Apply(user, target, parameters)`. Implementations live in the *game* (they know
  what `TActor` is).
- **`DataDrivenItem<TActor>`** — an item backed entirely by JSON. On `Apply` it
  runs every effect whose `trigger` matches the action, then decrements uses **iff**
  a `ConsumeItem` effect was present and uses are finite.
- **`ItemFactory<TActor>`** — loads items from JSON files/directories. Priority:
  a custom `handlerClass` (reflection) → otherwise `DataDrivenItem`. It validates
  that every referenced effect type is registered *before* returning the item.

The game closes the generic with global aliases so call sites read as plain
`IItem` / `IActionEffect`:

```csharp
global using IItem = Engine.Items.IItem<Game.Contracts.Entity>;
```

### 4.1 Effect catalog (game side, `Game.Services/Effects/`)
| Effect type | What it does |
|---|---|
| `Damage` | `target.TakeDamage(amount)` |
| `Heal` | `target.Stats.Health.Heal(amount)` |
| `Feed` | `target.Needs.Feed(amount)` (restores hunger) |
| `Relationship` | `user.EntityRelationships[target] += amount` |
| `ConsumeItem` | marker only — tells `DataDrivenItem` to decrement a use |

### 4.2 Item JSON shape (real example)
```json
{
  "itemId": "bread_loaf", "itemName": "Bread Loaf",
  "type": "Consumable", "weight": 0.4, "uses": 3,
  "enablesActions": ["Consume"],
  "effects": [
    { "type": "Heal", "trigger": "Consume", "parameters": { "amount": 15.0 } },
    { "type": "Feed", "trigger": "Consume", "parameters": { "amount": 25.0 } },
    { "type": "ConsumeItem", "trigger": "Consume", "parameters": {} }
  ]
}
```
Current data set (`data/items/`): `fist`, `kitchen_knife`, `rolling_pin`,
`broken_bottle`, `wine_bottle`, `bread_loaf`, `steak`, `coffee_cup`, `bouquet`,
`business_card`, `gift`, `blessing_herb`, `hex_token`.

> **Adding an item:** if it only combines existing effects, write a JSON file — no
> code. If it needs novel behavior, write a new `IActionEffect`, register it in
> `Program.cs`, and reference its keyword from the JSON. Reach for `handlerClass`
> only when an item needs genuinely bespoke logic the effect model can't express.

---

## 5. Space — `Engine.Spatial`

Deliberately minimal and **3D-proof** (the renderer decision is "keep groundwork
3D-ready, build the real renderer later, likely Silk.NET").

- **`Location`** — a named axis-aligned bounding box (`Vector3 Min`/`Max`) with a
  cached `Volume`. Axis convention: X = left/right, Y = front/back, Z = floor
  layer. `Contains(point)` is an inclusive bounds test.
- **`LocationRegistry`** — registers locations by unique tag; `FindLocation(pos)`
  returns the **smallest (most specific)** location containing a point, so nested
  zones (kitchen inside restaurant) resolve correctly. Linear scan, fine for <100
  zones.

Current world (`Program.cs`): `restaurant` (20×20) containing `kitchen` and
`dining_floor`. Spatial truth lives entirely in `Vector3` positions + AABBs;
there is **no pathfinding or collision by design**.

> Locations are registered in code at startup. JSON loading for zones is a
> deferred extension point, not a current system.

---

## 6. Identities — `Game.Contracts`

What an entity *is*. No logic beyond trivial self-mutators.

### 6.1 Entity
The actor. Holds: `ID`, `DisplayName`, `EntityType`; component bundles
`CombatStats`, `EntityPhysique`, `EntityNeeds`, `ActionModule`; a `HashSet` of
`BehaviorModule`; an `EntityRelationships` map (`otherId → int` affinity); an
`Inventory` of `IItem`; a `Vector3 Position`; and a nullable `PendingInfluence`.
`IsAlive` ⇔ current health > 0. Self-mutators: `TakeDamage`, health getters.

### 6.2 Stats & body
- **`CombatStats`** — STR/AGI/INT/CON/WIS/CHA plus an `EntityHealth`. Constructors
  give either fixed `10`s or random rolls (33% chance of a negative deviation,
  ±0–10). Most current systems read CON (via health) more than the others; the
  remaining stats are declared capacity for future mechanics.
- **`EntityHealth`** — `MaxHealth = CON*10 + Weight*0.2 − Height*0.1`. `TakeDamage`,
  `Heal`, and `UpdateFromStats` (rescales preserving the current ratio).
- **`EntityPhysique`** — `Weight`, `Height` (feed the health formula).

### 6.3 EntityNeeds — the mutable condition
Two needs drive Status weighting:
- **Hunger** (0–100, starts 100) — decays on hunger pulses.
- **Mood** (0–100, starts 75) — shifts from event fallout.
- Exposes `HungerRatio` / `MoodRatio` (0–1). Mutators: `TickHunger`, `Feed`,
  `AdjustMood` (clamped). Constants: `HungerDecayPerTick = 2`.

### 6.4 Behavior, Actions, and candidates
- **`BehaviorModule`** — a named trait: `Keywords`, `Affect`, `Scalar`.
  `MatchCount(actionKeywords)` counts keyword overlap. Contribution =
  `Affect * Scalar * matchCount`. *More overlap = stronger pull* — natural gradient.
- **`ActionModule`** — an entity's attachable action set (dictionary by name).
- **`IAction`** — `ActionName`, semantic `Keywords`, `RequiresItem`, an optional
  `BaseWeight` (default 0), and the verbs `GetViableItems`, `Execute`,
  `CanActorUse`, `CanTarget`. **Actions are stateless and shared.**
- **`ActionCandidate`** — one (action, target) pairing plus a list of
  `ActionWeight`s, each tagged with its `ActionWeightSource`
  (`Base | Behaviors | Relationship | Status | PlayerInfluence`). `TotalWeight`
  is their sum. The tagging is what lets the UI/announcer name the *primary factor*
  behind a decision.
- **`Influence`** — a player nudge: `TargetActionName`, `Strength`, `Source`.
  Stored on `Entity.PendingInfluence`, consumed when an action lands.
- **`DecisionRecord`** — `(ActionName, TargetName, PrimaryFactor)` history entry.
- **`GameSignal`** — `(Tick, Message)`; one line in the world announcement log.

---

## 7. The Decision Engine — `Game.Services`

This is the heart. `EntityService` knows **how** to turn an entity's state into a
chosen action; `StatusWeightCalculator` is the pure-function condition→weight map.
The controller calls these in a fixed pipeline (see §8.2).

### 7.1 Composite weight formula
```
CompositeWeight = Base + Behaviors + Relationship + Status + PlayerInfluence
```
Each term is added as a separately-tagged `ActionWeight` so its contribution stays
inspectable.

1. **Base** — `action.BaseWeight` (only `Idle` has one today: **8** — the floor
   every other action must outweigh).
2. **Behaviors** — Σ over the entity's behaviors of `Affect * Scalar * matchCount`
   against the candidate action's keywords.
3. **Relationship** — the raw affinity integer from `EntityRelationships[target]`,
   if any.
4. **Status** — from `StatusWeightCalculator` (next section).
5. **PlayerInfluence** — the pending nudge, conditionally (§7.3).

### 7.2 Status weighting (condition → keyword pushes)
`StatusWeightCalculator.Apply` converts HP / Hunger / Mood ratios into additive,
**keyword-matched** weights. It never checks action names — a new action with the
right keywords automatically picks up every relevant modifier.

Three bands per need:
- **Sweet spot (0.50–0.75):** normal behavior, maximum player control.
- **Deficit (ratio < 0.50):** self-preserving pushes; a second, harsher **critical**
  tier kicks in below **0.25**.
- **Exceeded (ratio > 0.75):** overindulgent pushes (giddy / complacent).

Magnitude scales with how far into the band the ratio is, times a per-keyword
scale, times match count. Current rule set:

| Condition | Boosts | Penalties |
|---|---|---|
| **Low HP** (<0.50) | SelfPreserving (×8), Defensive (×6) | <0.25: Offensive/Aggressive (×5) |
| **Low Hunger** (<0.50) | Greedy (×12), SelfPreserving (×4) | <0.25: Offensive/Aggressive (×3), Social/Diplomatic (×3) |
| **Low Mood** (<0.50) | SelfPreserving (×5) | Social/Diplomatic (×6) |
| **Giddy Mood** (>0.75) | Social/Diplomatic (×5) | Aggressive/Violent (×4) |
| **Stuffed Hunger** (>0.75) | Social (×3) | Aggressive/Violent (×3), Defensive (×2) |

> A starving entity stops socializing and fixates on eating; a near-death entity
> stops attacking and turtles; a giddy entity gets chatty and won't pick fights.
> These read as personality but are pure keyword math.

### 7.3 Player influence resolution
`ApplyInfluence` consumes `PendingInfluence` for matching candidates with two
gates:
- **Mood gate:** `effectiveStrength = Strength * MoodRatio`. A sour entity barely
  listens to the narrator.
- **Stranger gate:** non-favored pawns divide strength by `StrangerResistDivisor`
  (2). The narrator acts *through* its favored pawn; everyone else half-listens.
- **In-character gate:** if the nudged action has **no positive Behaviors weight**
  (out of character), the influence is halved (min 1). Influence *biases*, it does
  not *command*.

The nudge is consumed the moment an action actually lands (not on movement beats).

### 7.4 Selection — weighted random draw
`SelectWeighted` does a single roll over `Σ max(1, TotalWeight)`. The
**`max(1, …)` floor guarantees every candidate keeps a nonzero chance**, which
prevents deadlocks where penalties zero everything out. This is the "determinism
with texture" pillar in code — never argmax.

`SelectItem` then picks a random viable item for the chosen action.

### 7.5 Mood fallout
`ApplyMoodFallout` adjusts mood from the executed action's keywords:
Violent −10 to target, else Aggressive −6; Supportive +4, Diplomatic +3 to target;
Greedy self-action +3 to actor. Keyword-driven, like everything else.

### 7.6 Movement (conceptual spatial sim)
- `IsInRange(a, b, range)` — Euclidean distance test.
- `MoveToward(entity, target, speed, stopRange)` — steps toward the target,
  stopping just inside range, clamped to world bounds; returns remaining distance.
- `Wander(entity, step)` — random drift (used by Idle).
- `LocationTag(entity)` — the most-specific location name, or `"?"`.
- World bounds are derived from the union of all registered locations.

### 7.7 Standard actions (`Game.Services/Actions/`)
| Action | Keywords | Item? | Targeting |
|---|---|---|---|
| **Attack** | Aggressive, Violent, Offensive | required | others |
| **Defend** | SelfPreserving, Reactive, Defensive | required | self |
| **Grapple** | Aggressive, Reactive, Defensive | required | others |
| **Consume** | SelfPreserving, Greedy | required | self |
| **Speak** | Social, Diplomatic | required | others (gifts a depleted item to the target) |
| **Enchant** | Social, Supportive | required | others |
| **Idle** | SelfPreserving | none, `BaseWeight 8` | self (recovers +2 mood) |

Actions gate availability through items: `CanActorUse` checks the inventory has
something enabling the action. This is the "items as verbs" pillar enforced.

---

## 8. Orchestration — `EntityARestaurantGame`

### 8.1 Composition root (`Program.cs`)
The only place state is wired: register effects → build the item factory → register
locations → construct entities (inventory, behaviors, actions, position) → build
`EntityService(locations)` → build `EntityController` → add entities → start the UI
on `PawnSelectScreen`. Also defines `SpawnNobody()` (the fresh-drifter factory for
start-anew).

### 8.2 EntityController — the "when"
Owns the `Clock`, `Wheel`, `SimLoop`, the active/inactive rosters, per-entity
decision events, decision history, and the signal log. The UI talks only to it.

**Pacing constants** (the dials you tune for feel):
| Constant | Value | Meaning |
|---|---|---|
| `TicksPerSecond` | 2 | real-time auto-run speed |
| `HungerPulseInterval` | 10 | ticks between hunger beats |
| `HungerPerPulse` | 5 | hunger lost per pulse |
| `StarvationDamage` | 3 | HP lost per pulse at 0 hunger |
| `DecisionInterval` | 5 | ticks between one entity's decisions |
| `InteractionRange` | 3 | max distance to act on a target |
| `MoveSpeedPerDecision` | 2.5 | step length when approaching |
| `StrangerResistDivisor` | 2 | non-favored influence divisor |

**Two clocks of behavior**, both events on the wheel:
- `HungerPulseEvent` (`WorldObligation`, every 10 ticks) — decays everyone's
  hunger; starving entities take HP; then a death sweep.
- `EntityDecisionEvent` (`SelfMaintenance`, per entity every 5 ticks, **staggered**
  so entities don't act in lockstep) — calls `ActOnce`.

**`ActOnce` — the decision beat** (this is the pipeline that ties §7 together):
1. `BuildCandidates` over all active entities.
2. `ApplyBehaviorWeights` → `ApplyStatusWeights` → `ApplyInfluence`.
3. `SelectWeighted`.
4. **Spatial gate:** if the target is out of `InteractionRange`, this beat becomes
   an **Approach** (move toward it). *Intent and influence persist* — the entity
   keeps wanting it until close enough to act.
5. Otherwise pick an item, `Execute`, apply mood fallout, record + announce, sweep
   deaths. `Idle` also triggers a `Wander` and is not announced ("resting isn't
   news").

**Death handling:** after every act/pulse, `SweepDeaths` moves dead entities to
inactive and **lazy-cancels their decision event** (it reaches its bucket and
no-ops). Going inactive is the same mechanism.

**Two time modes** the UI drives:
- **Step** (`AdvanceToNextBeat`) — advances tick-by-tick until ≥1 event fires
  (idle ticks are free), so a keypress jumps straight to the next beat.
- **Real-time** (`Pump`) — the accumulator runs at 2 ticks/sec.

**Favored pawn:** `FavoredEntityId` is the pawn the narrator acts through.
`FavoredEntity` resolves it from the active roster (null ⇒ it died).

**Signal log:** `Announce` appends a `GameSignal`, capped at 50 lines — the read
model the UI renders.

### 8.3 UI (console, debug-tier)
A small screen-stack presentation layer. It is explicitly **disposable** — the
real renderer (likely Silk.NET 3D) replaces it later; the ASCII map is labeled
debug-only.

- **`ScreenManager`** — a `Stack<IScreen>`. **Pushing any non-realtime screen over
  a realtime one pauses the world for free** (the accumulator stops being fed). In
  real-time mode it pumps the sim between renders until a key arrives.
- **`IScreen` / `IRealtimeScreen`** — render + input; realtime screens add `Pump`.
- **`ScreenAction`** — sealed hierarchy: `Stay / Pop / Quit / Push / Replace`.
- **Screens:** `PawnSelectScreen` (intro — pick your favored pawn) → `WorldScreen`
  (the live world: roster, debug map, event log, Step/Auto-Run/Inspect/Influence;
  Space = next beat, A = toggle real-time). `EntityListScreen`,
  `EntityInspectScreen`, `InfluenceScreen`/`InfluenceResultScreen` (apply a nudge),
  and `StartAnewScreen` (on favored death: settle behind a survivor or spawn a
  fresh "Drifter" nobody).

### 8.4 SmokeTest harness (`tools/SmokeTest`)
A headless driver that runs the controller without the console UI (the console UI
can't run with redirected input). Use it to exercise the sim in tests/CI.

---

## 9. The Rendering Boundary (future 3D)

Sim truth is renderer-free **by rule**: positions are `Vector3`, zones are AABBs,
movement is vector math — all 3D-ready. The `RenderCue`/cue-sink pattern from
`specs/archive/docs-6-11/TickSystem.cs` is **deliberately unported**; it becomes the render
interface when a real renderer arrives. Until then: conceptual interactions plus
the debug ASCII map. **Do not build elaborate console/2D presentation systems** —
they would be throwaway work. Any debug view must read sim truth and stay disposable.

---

## 10. Cross-Cutting Invariants (don't break these)

- **Keywords over names.** Never branch on an action's name inside weight code.
  Declare keywords; let the matchers pick it up. New actions/behaviors/status rules
  integrate by *tagging*, not by editing the engine.
- **Weighted draw, never argmax.** Keep the `max(1, weight)` floor so nothing ever
  becomes truly impossible (deadlock guard).
- **Influence biases, never commands.** Respect the mood / stranger / in-character
  gates. The player is a narrator, not a puppeteer.
- **Events fire by arrival, not by polling.** Recurring behavior is an `IEvent`;
  re-arm drift-free (`FireTick + interval`) and within the 32767-tick horizon.
- **Layer discipline.** Identity → Contracts (no logic), how-to → Services (no
  state), when → Controller, reusable machinery → `Engine.*` (no game concepts).
  UI talks only to the controller.
- **Items carry actions.** Most actions require an enabling item; keep "items as
  verbs" intact when adding interactions.

---

## 11. Known Gaps & Planned Extensions

**Intentional gaps (declared but unused today):**
- `EventTimeline` tags are set but the **dormancy layer that consumes them doesn't
  exist yet**.
- `RenderCue`/cue-sink not ported (the renderer interface, deferred).
- Most `CombatStats` (STR/AGI/INT/WIS/CHA) are capacity not yet read by mechanics.
- No pathfinding/collision (by design — conceptual spatial sim only).
- Zone loading from JSON is a deferred extension point; locations are coded today.

**Roadmap (from `project_roadmap` — all three lean on the keyword system):**
1. **Sub-actions** — what an action *means in context* (Attack a near-dead enemy vs.
   a healthy one; Grapple as de-escalation vs. setup; Speak as gift/threat/negotiate).
   Refines *how* an action resolves.
2. **Expanded world variables** — environmental context feeding Status weights:
   ally/enemy state (ally low HP → Protective spikes), location modifiers (kitchen
   boosts Consume), proximity, situational awareness.
3. **Action result propagation** — actions ripple beyond the direct target:
   witnesses shift relationships, bystander mood changes, a successful Speak
   cascades cooperation. The **dialog/speech system** (`specs/archive/docs-6-11`,
   "SpeechSystem_WIP") is the next discussion.

Each is meant to *add or read keywords*, not add hardcoded branches — sub-actions
define *what happened*, world variables define *the context*, propagation defines
*how the world reacts*.
```
