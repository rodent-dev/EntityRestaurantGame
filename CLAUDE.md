# EntityARestaurantGame Development Guidelines

Last updated: 2026-07-16 (rewritten by hand — the previous version was stale
speckit template output).

## What this is

A systemic sim game (C# 10 / .NET 10.0): autonomous entities with behaviors,
minds, and dialog inside a restaurant world, rendered either as a console UI
(debug fallback, default) or a Silk.NET OpenGL 3D view with a 2D interface
(`--3d`). Deliberate PS2-era look: 480×270 canvas, nearest upscale.

## Documentation map (read these before large changes)

- `ARCHITECTURE.md` — layering rules, project map, dependency direction
- `STATUS.md` — current state of every system, with layered mermaid diagrams
- `RECOMMENDATIONS.md` — formal review findings + prioritized pickup points
- `specs/active/UI_2D_Interface_Plan.md` — UI component inventory and build order
- `DESIGN.md` — sim design pillars
- `specs/implemented/Dialog_*.md` — the dialog system as built
- `specs/implemented/Activities_And_Events.md` — long-running actions (a job is
  a commitment paid on performance) and the event distributor (who noticed
  what); both ported from the sibling Luna project
- `specs/implemented/Action_Wheel.md` — the player's verb surface: keyword-
  addressed whispers, shared taxonomy rings, blocked entries with reasons

Specs are filed by state: `specs/active/` (designed, not yet built),
`specs/implemented/` (shipped — reference for how it works), `specs/archive/`
(superseded; historical snapshots kept for context).

## Commands

```
dotnet build src\EntityARestaurantGame\EntityARestaurantGame.csproj   # builds everything via refs
dotnet run --project src\EntityARestaurantGame                        # console UI (default)
dotnet run --project src\EntityARestaurantGame -- --3d                # 3D + 2D interface
dotnet run --project src\EntityARestaurantGame -- --3d --smoke       # auto-close + smoke.bmp
dotnet run --project src\EntityARestaurantGame -- --gen 42 --3d      # procedural world (seeded)
dotnet run --project tools\SmokeTest                                  # headless sim exercise
dotnet run --project tools\SoakTest -- 42 4000 5                      # long run; 3rd arg = sim seed (reproducible)
dotnet run --project tools\DecisionProbe -- 42 500 1                  # where the decision probability went
dotnet run --project tools\DecisionProbe -- 42 500 1 --selftest       # determinism guard
```

**Reproducibility**: worldgen and the run are seeded separately —
`BuildGenerated(worldSeed, simSeed)`. Omit `simSeed` for a random run; supply
it to A/B a balance change. See RECOMMENDATIONS.md P2.21 for the four things
that used to break it (and the rule about `new()` hiding RNG from grep).

## Structure

```
src/Engine.*        generic, reusable, never reference game code
src/Game.Contracts  entity state only (trivial self-mutators at most)
src/Game.Services   all sim logic (HOW)
src/EntityARestaurantGame  composition root, EntityController (WHEN), UIs
tools/SmokeTest     headless harness
tools/SoakTest      long-run stability harness
data/               items, entities, world/locations, shaders, input bindings,
                    models, ui images, fonts — content is JSON/asset files
specs/              design docs, filed active/ implemented/ archive/
```

The `old/` archived snapshots were removed on 2026-07-30; they remain on the
`baseline_old` branch if ever needed.

## Hard rules

- **Layering**: Contracts hold identity/state, Services control objects,
  Controllers decide timing. Engine.* stays game-agnostic.
- **Renderers only READ sim truth**; the sim never references rendering.
- **Keywords, not names**: weighting systems (status, mood, behaviors) match
  action keywords — never test `ActionName` in a weight system.
- **An action that takes time is defended by comparison, never by a roll.**
  `CommitmentCalculator` prices staying; the controller compares it against the
  challenger's weight. Folding the incumbent into the weighted roll makes
  abandonment compound to a certainty over the duration.
- **The player's whisper is keyword-addressed.** `Influence.Keywords` comes
  off the verb the player picked; `ActionTaxonomy.Answers` matches a candidate
  when it carries all of them. Never reintroduce name matching — that is what
  made the action wheel impossible.
- **Announcements are typed facts with an audience.** `Announce`/`Publish` build
  a `WorldEvent` and the `EventDistributor` decides who noticed. Reactions match
  `WorldEvent.Keywords`, never `ActionName`.
- **Regions/settlements are NOT UI scenes** — they belong to a separate
  future world-view controller, not the Engine.Ui SceneManager.
- **No new NuGet packages without presenting vetted alternatives first**
  (standard option / heavyweight / zero-dep path) — the user decides.
- Tech: C# 10, .NET 10.0, Silk.NET 2.23, StbImageSharp, StbTrueTypeSharp.
  Everything else is BCL.
