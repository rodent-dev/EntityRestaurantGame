# Code & Design Review — Recommendations

Review date: **2026-07-16**. Companion doc: [STATUS.md](STATUS.md) (what exists
and how it fits together). This file is deliberately separate so it can be
revisited and checked off over time.

Method: full read of the rendering/UI/input tier, agent-assisted sweep of the
sim tier (Contracts, Services, Ticks, Items, Spatial, controller, console UI,
item data), plus the running smoke evidence.

---

## Part 1 — What went well (keep doing these)

1. **The layering doctrine is real, not aspirational.** Contracts/Services/
   Controller separation is compiler-enforced via project references, and it
   held up in practice: bolting a 3D renderer + 2D UI onto the sim required
   **zero sim changes**. That's the payoff of "renderers only read sim truth."
2. **Keyword systems over name checks.** `StatusWeightCalculator`, mood
   fallout, and behavior matching never test action names — a new keyworded
   action automatically participates in every weighting system. This is the
   project's best extensibility property; protect it.
3. **The timer wheel is disciplined.** Wrap-aware tick math, drift-free
   rescheduling (`FireTick + interval`, not `now + interval`), lazy
   cancellation, deterministic in-bucket ordering. "Pause = stop feeding the
   accumulator" fell out of the design for free in both UIs.
4. **Data-driven items validate at load.** Unknown effect types fail at item
   load, not mid-combat. The descriptor block doing double duty (effects for
   the body, axis valences for the mind) is elegant.
5. **Pattern symmetry across UIs.** Console `IScreen`/`ScreenAction` and
   windowed `Scene`/`SceneAction` are the same idea; anyone who learns one
   knows the other.
6. **Docs travel with the code.** ARCHITECTURE.md, DESIGN.md, the dialog map,
   and the specs folder describe the system as it is (as of this date), not as
   it was three refactors ago.

## Part 2 — What is not as good (honest findings)

### Repository hygiene
- **P2.1 — Not a git repository.** No history, no rollback, no branches. One
  bad disk day loses everything. This is the single highest-risk finding.
- **P2.2 — `CLAUDE.md` is stale template output.** It lists `pytest; ruff`
  as commands (wrong ecosystem), `backend/ frontend/` structure (doesn't
  exist), and "NEEDS CLARIFICATION" placeholders. Misleads any tooling/agent
  that trusts it.
- **P2.3 — No solution file and no CI.** Building means knowing which csproj
  to name; nothing runs the smoke tests automatically.

### Duplication / drift hazards
- ~~**P2.4 — World setup duplicated.**~~ **Fixed 2026-07-17:** both Programs
  now build through `World/WorldBuilder.cs`. (The drift was real — SmokeTest's
  merchant had already lost the `guard: 5` relationship Program.cs had.)
- ~~**P2.5 — Entities and locations are hardcoded.**~~ **Fixed 2026-07-17:**
  locations live in `data/world/locations.json`, entities in
  `data/entities/*.json` (drifter archetype in `entities/templates/`).
  Definitions validate at load like items do: unknown actions and axis typos
  throw immediately. Adding a resident = adding a JSON file.

### Dead / half-wired code (found in the sweep)
- ~~**P2.6 — `EventTimeline`**~~ **Removed 2026-07-17** (enum + `Timeline` on
  IEvent and all three events). A breadcrumb comment in `IEvent.cs` points to
  `specs/archive/docs-6-11/EventExamples.md` for when the dormancy layer lands.
- ~~**P2.7 — `IntentType.Question` / `Quest`**~~ **Removed 2026-07-17.**
- ~~**P2.8 — `ItemFactory.HandlerClass`**~~ **Removed 2026-07-17** (DTO field
  + reflection branch).
- ~~**P2.9 — Dead data fields**~~ **Fixed 2026-07-17:** `isEquipped` /
  `speakEffectModifier` stripped from 6 item JSONs, and all JSON DTOs (items,
  effects, entities, locations) now use `JsonUnmappedMemberHandling.Disallow`
  — a typo'd field is a load-time error, not a silent no-op.
- ~~**P2.10 — Unused members**~~ **Removed 2026-07-17:** `EntityPhysique` (class
  + `Entity.Physique`), `EntityHealth.UpdateFromStats`, the 6-arg `CombatStats`
  ctor, and `IItem.IsDepleted`. `gift.json`/`broken_bottle.json` deliberately
  KEPT — they're valid inert content, loadable by id when an inventory wants
  them.
- **P2.11 — `EnchantAction` mood quirk**: its `Supportive` keyword grants the
  *target* +4 mood via fallout even when the item is a curse (`hex_token`) —
  fallout keys off the action, not the item's intent. Decide if that's flavor
  or bug. *(Left open on purpose — live behavior, needs a design call, not a
  deletion.)*
- **P2.19 — Rethink the status weight bands** *(user-flagged 2026-07-18;
  balancing, important)*: the deficit band boosts SelfPreserving / Defensive /
  Greedy keywords indiscriminately, so a starving entity finds *Defend* (which
  triple-matches) more tempting than actions that would fix the deficit —
  soak runs show dying entities spamming Defend instead of eating/grazing/
  working. Direction to explore: deficit boosts should favor actions that
  *address the failing need* (hunger → Greedy/food-producing; low HP →
  Defensive; low mood → Social), not a flat keyword basket. Revisit alongside
  the first balance pass of the purpose systems.
- **P2.20 — Mind traits are shotgun-surgery** *(user-flagged 2026-07-18)*:
  adding `Sociability` required six code sites (MindModule property,
  MindDefinition + MindRange DTO mirrors, WorldBuilder copy, generator
  sample + validation). Traits should be a dictionary like `Preferences`
  already is — `Mind.Traits` (name → double, defaulted reads), DTOs carry
  `traits: {name: value}` / `{name: [min,max]}`, and the copy/sample/validate
  loops get written once. Skepticism/Impulsiveness/Sociability become
  accessors over the dict for call-site compatibility. After that, a new
  trait = one read site + data. Do this before the NEXT trait, not after.
- ~~**P2.18 — Process lingered after window close.**~~ **Found & fixed
  2026-07-17:** the Silk input context was never disposed; its device threads
  could keep `EntityARestaurantGame.exe` alive minutes after the window
  closed (caught red-handed locking DLLs during a rebuild — likely the real
  source of the "ghost processes" observation, alongside MSBuild node reuse).
  `Render3DHost.Run()` now disposes the input context on teardown; verified
  0 processes 2s after exit. `GameWindow`'s auto-close timer (orphaned by the
  smoke-race fix) removed in the same pass.

### Testing
- **P2.12 — No assertion-based tests.** The smoke harness prints a narrative
  and the 3D smoke writes a screenshot — good *evidence*, but nothing fails
  automatically when weights, dialog math, or the wheel regress. The most
  test-worthy code (MindMath, StatusWeightCalculator, Wheel, ObjLoader,
  BindingSet round-trip) is pure and trivially testable.

### Smaller technical debts
- **P2.13 — Signal log caps at 50 with no persistence** — long-run debugging
  loses history; the debug HUD can only show what survived.
- **P2.14 — `InputState` lacks absolute mouse position** (delta only) —
  needed for mouse-driven menus and 3D picking.
- **P2.15 — Canvas 480×270 and pacing constants are hardcoded** — fine now,
  but the settings screen will want internal resolution / upscale mode, and
  sim pacing lives in `const` (not tunable per save).
- **P2.16 — `SpriteFont` bakes ASCII 32–126 only** — no accented characters;
  fine until a name or flavor text needs one.
- **P2.17 — Console `ScreenManager.Run` uses `Console.ReadKey`** — safe, but
  it means the console fallback can't be driven by automated tests either.

## Part 3 — Recommendations (prioritized pickup points)

Ordered so each item is a self-contained session you can pick up.

### R1. `git init` + first commit *(30 min, do before anything else)*
`git init`, a `.gitignore` for `bin/ obj/ *.user smoke.bmp`, one commit named
"pre-history import". Optionally tag what's in `old/` then delete it from the
working tree — git becomes the archive. Fixes P2.1, shrinks the repo.

### R2. Rewrite `CLAUDE.md` honestly *(15 min)*
Real commands (`dotnet build/run` lines from STATUS.md), real structure,
pointer to ARCHITECTURE/STATUS/RECOMMENDATIONS. Fixes P2.2.

### ~~R3. Extract `WorldBuilder`~~ ✅ Done 2026-07-17
`World/WorldBuilder.cs` builds the world for both Programs, and went straight
to JSON: `data/world/locations.json` + `data/entities/*.json` with load-time
validation. Both P2.4 and P2.5 closed in one pass. Remaining nicety for
later: entity JSONs could gain spawn markers (active/inactive) and multiple
templates when content grows.

### R4. First real test project *(1 session, big leverage)*
`tests/Engine.Tests` (xunit): Wheel wrap/cancel/reschedule, `Clock.IsDue`
boundaries, `StatusWeightCalculator` bands, `MindMath.Sat/SoftmaxPick`
determinism with seeded RNG, `ObjLoader` malformed-input cases, `BindingSet`
JSON round-trip, `ItemFactory` validation failures. All pure — no mocking.
Fixes P2.12. (The strict-deserialization half of this item shipped early with
the P2.9 fix — tests should still cover the failure cases.)

### R5. Continue the UI build order *(multi-session, spec exists)*
Per `specs/active/UI_2D_Interface_Plan.md`: title screen → real settings pages
(video page owns canvas-resolution setting, fixing part of P2.15) → promote
the DebugHud signal ticker into the player-facing toast/HUD → interaction
menu + inventory (first gameplay through the new UI). TextBox needs char
events plumbed through `Engine.Input` first (small, isolated addition —
pairs naturally with absolute mouse position, P2.14).

### ~~R6. Decide the dormant seams~~ ✅ Done 2026-07-17 (except P2.11)
P2.6–P2.10 deleted (see strikes above); strict JSON deserialization added as
the follow-through. Still open: **P2.11**, a design call not a deletion —
suggest fallout should consult the item's descriptor valence (curse ⇒
negative), which keeps the keyword system pure.

### R7. Save/load groundwork *(later, before content grows)*
The sim state is already well-partitioned (Entity graph + wheel schedule +
clock tick). A JSON snapshot of rosters/minds/inventories + rescheduling on
load is feasible now and gets exponentially harder as systems accrue. Signal
log persistence (P2.13) rides along.

### R8. Renderer niceties *(opportunistic, low priority)*
Absolute mouse position + picking ray (unlocks click-a-pawn → inspect, the
3D equivalent of the console's EntityListScreen); text batching in
`Renderer2D` if glyph draw calls ever show up in frame time (they won't at
menu scale); PNG screenshots.

---

*Revisit ritual: open this file, pick the highest unchecked R-item, strike
through what's done, add new findings at the bottom of Part 2.*
