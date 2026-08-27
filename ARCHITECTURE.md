# Repository Architecture

Layering follows the API-style philosophy: **Contracts hold object identities,
Services control objects (fetching, building, mutating), Controllers handle the
API — the reachable combinations of services.**

```
src/
├── Engine.Ticks/          # GENERIC — timer substrate (Clock, Wheel, SimLoop, IEvent)
├── Engine.Items/          # GENERIC — item pipeline (IItem<TActor>, ItemDefinition,
│                          #   ItemFactory<TActor>, EffectRegistry<TActor>, DataDrivenItem<TActor>)
├── Engine.Spatial/        # GENERIC — AABB locations (Location, LocationRegistry)
├── Engine.Render/         # GENERIC — Silk.NET OpenGL shell (GameWindow, Shader,
│                          #   GpuMesh, Primitives, ObjLoader, OrbitCamera,
│                          #   RenderTarget, Screenshot)
├── Engine.Input/          # GENERIC — action-mapped input (BindingSet<TAction>,
│                          #   InputState<TAction>, SilkInputAdapter<TAction>)
├── Engine.Ui/             # GENERIC — 2D scene controller (Scene, SceneAction,
│                          #   SceneManager, UiContext, UiInput, MenuList);
│                          #   references Engine.Render only
│
├── Game.Contracts/        # IDENTITIES — Entity, EntityNeeds, BehaviorModule, IAction,
│                          #   ActionCandidate, Influence, DecisionRecord, GameSignal,
│                          #   Activity (a run in progress), Commitment,
│                          #   WorldEvent + IEventReaction (the typed ledger row),
│                          #   ActionTaxonomy (the keyword vocabulary + player rings)
├── Game.Services/         # HOW — EntityService (candidates, weights, selection, needs,
│                          #   mood fallout, movement, Focus), StatusWeightCalculator,
│                          #   CommitmentCalculator (is anything worth stopping for),
│                          #   EventDistributor (who noticed what),
│                          #   PlayerAffordances (the curated verb surface + reasons),
│                          #   standard Actions/ and item Effects/
└── EntityARestaurantGame/ # THE GAME — Program (composition root), EntityController
                           #   (WHEN: owns clock/wheel/rosters/signal log),
                           #   Simulation/ (tick events), UI/ (console screens),
                           #   Render3D/ (Render3DHost — the 3D composition point),
                           #   Scenes/ (2D interface scenes: pause, settings, …)
tools/SmokeTest/           # headless harness driving the controller without the UI
tools/SoakTest/            # long-run stability harness (settlement survival)
data/items/                # item JSON, linked into the game's output at build
data/shaders/              # GLSL (basic.vert/frag), linked into output at build
data/input/                # bindings.json — action-to-device control bindings
data/models/               # Wavefront OBJ assets (pyramid.obj)
data/ui/                   # menu background images (any PNG/JPG/BMP/TGA)
data/fonts/                # drop-in .ttf overrides the game font (system serif fallback)
specs/active/              # designed, not yet built
specs/implemented/         # shipped — reference for how each system works
specs/archive/             # superseded (speckit 001, docs-6-11 snapshots)
```

## Dependency rules (compiler-enforced via project references)

- `Engine.*` reference **nothing** — they are reusable in any project.
- `Engine.Items` is generic over `TActor`; it never inspects the actor, only
  routes it to effects. The game closes the generic with
  `global using IItem = Engine.Items.IItem<Game.Contracts.Entity>;` (see each
  project's `GlobalUsings.cs`), so call sites read as plain `IItem`.
- `Game.Contracts` references only `Engine.Items` (inventory/action signatures).
  No logic beyond trivial self-mutators (`TakeDamage`, `Feed`, `AdjustMood`).
- `Game.Services` references Contracts + the engines it operates with.
  `EntityService` knows **how** to do everything to an entity; it holds no
  game state of its own (only randomness + world geometry).
- `EntityARestaurantGame` is the composition root: builds the world, owns time
  (the controller schedules events; events call back into the controller, which
  delegates to services), and renders. **Custom items, custom effects, and
  game-specific logic belong here or in Game.Services — never in Engine.***

## Controller vs. Service in one sentence

`EntityController` decides **when** (decision beats, hunger pulses, death
sweeps, what gets announced, when an activity begins/advances/settles);
`EntityService` knows **how** (build candidates, weigh them, move a pawn, decay
hunger, price focus). UI talks only to the controller.

## Activities and the event spine

Two rules that cut across the layering, both landed 2026-08-26 — see
`specs/implemented/Activities_And_Events.md`:

- **An action may take several beats.** `IAction.Duration`, `Sustain` and
  `Resolve` are default interface members, so an action that says nothing is
  instantaneous and behaves exactly as before. The *run* is `Entity.Current`
  (state, in Contracts); the *decision to stay* is `CommitmentCalculator`
  (logic, in Services); *begin/advance/settle* is the controller (timing).
- **The player addresses the sim in keywords, never action names.**
  `Influence` carries the keywords of whatever verb the player pointed at, and
  a candidate answers it when its action carries all of them
  (`ActionTaxonomy.Answers`). The rings the wheel shows and the hostile/warm
  groups relationship weighting uses are the same table, so a new action lands
  in the right drawer for free. See `specs/implemented/Action_Wheel.md`.
- **Every announcement is a typed fact with an audience.** `Announce` builds a
  `WorldEvent` and hands it to `EventDistributor`, which works out who was the
  actor, who was a participant, and who merely witnessed it, then runs the
  registered `IEventReaction`s. Prose is a rendering carried alongside the
  facts; reactions match `WorldEvent.Keywords`, never `ActionName`.

## Rendering boundary (3D)

Sim truth is renderer-free by rule. The rudimentary Silk.NET pipeline exists:
`Engine.Render` + `Engine.Input` are generic (reference no game code);
`Render3DHost` in the game project is the composition point — it drives the
sim clock via `EntityController.Pump(dt)` and only ever **reads** sim truth
(positions, entity types). Sim axes are X=left/right, Y=front/back,
Z=floor-layer; render axes are X=right, Y=up, Z=toward-camera, so
`renderPos = (sim.X, sim.Z, sim.Y)`.

Run it with `dotnet run --project src\EntityARestaurantGame -- --3d`
(add `--smoke` to auto-close after 3s and dump a `smoke.bmp` screenshot).
Controls live in `data/input/bindings.json`: WASD pans, right-drag orbits,
scroll zooms, Escape opens the pause menu. The scene renders into a 480×270
offscreen target and upscales with nearest filtering (deliberate PS2-era
look). The console WorldScreen ASCII map remains as a debug view of the same
sim truth.

## 2D interface (Engine.Ui)

The windowed counterpart of the console screen stack — see
`specs/active/UI_2D_Interface_Plan.md` for the full component inventory and build
order. `SceneManager` holds a stack of `Scene`s above the 3D world; the top
scene gets a device-free `UiInput`, returns a `SceneAction`
(Stay/Pop/Push/Replace/Quit), and while any sim-blocking scene is on the
stack the host stops pumping the sim (the console `IRealtimeScreen` trick).
UI draws into the same 480×270 target as the world, so it inherits the
upscale. Text is a TTF baked to a glyph atlas at load (`SpriteFont`, via
StbTrueTypeSharp); menu backgrounds are plain image files in `data/ui/`
loaded through `Texture2D` (StbImageSharp). **Regions/settlements/world-map
views are NOT scenes in this controller** — they get their own controller
(project decision, 2026-07-16).
