# EntityARestaurantGame

[![build](https://github.com/rodent-dev/EntityRestaurantGame/actions/workflows/build.yml/badge.svg)](https://github.com/rodent-dev/EntityRestaurantGame/actions/workflows/build.yml)

A systemic sim game (C# 10 / .NET 10.0): autonomous entities with behaviors,
minds, and dialog inside a restaurant world. Renders as either a console UI
(debug fallback, default) or a Silk.NET OpenGL 3D view with a 2D interface.
Deliberate PS2-era look — 480×270 canvas, nearest-neighbour upscale.

## Running

```bash
dotnet build EntityARestaurantGame.slnx                            # everything
dotnet run --project src/EntityARestaurantGame                     # console UI (default)
dotnet run --project src/EntityARestaurantGame -- --3d             # 3D + 2D interface
dotnet run --project src/EntityARestaurantGame -- --3d --smoke     # auto-close + smoke.bmp
dotnet run --project src/EntityARestaurantGame -- --gen 42 --3d    # procedural world (seeded)
dotnet run --project tools/SmokeTest                               # headless sim exercise
```

## Layout

| Path | What |
|------|------|
| `src/Engine.*` | Generic and reusable; never references game code |
| `src/Game.Contracts` | Entity state and identity only |
| `src/Game.Services` | All sim logic (the HOW) |
| `src/EntityARestaurantGame` | Composition root, EntityController (the WHEN), UIs |
| `tools/` | `SmokeTest` (headless exercise), `SoakTest` (long-run stability) |
| `data/` | Content — items, entities, world, shaders, models, fonts, bindings |
| `specs/` | Design docs, filed by state (see below) |

Specs are filed by state: `active/` (designed, not yet built),
`implemented/` (shipped — reference for how it works), `archive/` (superseded).

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) — layering rules, project map, dependency direction
- [STATUS.md](STATUS.md) — current state of every system
- [RECOMMENDATIONS.md](RECOMMENDATIONS.md) — review findings and pickup points
- [DESIGN.md](DESIGN.md) — sim design pillars
- [CLAUDE.md](CLAUDE.md) — working guidelines and hard rules

## Requirements

.NET 10.0 SDK. Dependencies are Silk.NET 2.23, StbImageSharp, and
StbTrueTypeSharp — everything else is BCL.
