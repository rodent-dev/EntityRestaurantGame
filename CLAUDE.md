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
```

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
- **Regions/settlements are NOT UI scenes** — they belong to a separate
  future world-view controller, not the Engine.Ui SceneManager.
- **No new NuGet packages without presenting vetted alternatives first**
  (standard option / heavyweight / zero-dep path) — the user decides.
- Tech: C# 10, .NET 10.0, Silk.NET 2.23, StbImageSharp, StbTrueTypeSharp.
  Everything else is BCL.
