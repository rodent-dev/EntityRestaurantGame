# Implementation Plan: Console UI System

**Branch**: `001-entity-influence-engine` | **Date**: 2026-03-19 | **Spec**: [spec.md](./spec.md)
**Input**: Barebones RPG-style console UI with flexible screen stack, context-sensitive option
menus, and player influence commands — inspired by RPG Maker's window/screen model.

---

## Summary

Build a screen-stack UI system inside the console project. Each screen owns its rendering and
input handling and returns a `ScreenAction` (push/pop/replace/stay/quit) rather than calling
navigation directly. `ScreenManager` owns the stack and the read-key loop. Five concrete screens
cover the full player journey: World → EntityList → EntityInspect → Influence → InfluenceResult.
The `Influence` domain object is added to `EntityObjects` so `EntityController.ProcessTick()` can
consume player-injected weight boosts before clearing them.

---

## Technical Context

**Language/Version**: C# 10 / .NET 10.0
**Primary Dependencies**: `System.Console` (built-in); `System.Numerics.Vector3` (built-in, from location system)
**Storage**: N/A — all UI state is in-memory
**Testing**: Manual play-test scenarios in `quickstart.md` (10 scenarios)
**Target Platform**: Windows console — Windows Terminal recommended for Unicode box characters
**Project Type**: UI layer lives entirely in `EntityARestaurantGame/` (console project); domain additions (`Influence`, `PendingInfluence`, `PlayerInfluence` weight source) go in `EntityObjects/`
**Performance Goals**: < 16ms per render (imperceptible on full clear-and-redraw at this entity count)
**Constraints**: Screens MUST NOT reference `System.Console` directly — all output goes through `ConsoleRenderer`. Core engine (`EntityObjects`) MUST remain console-agnostic (FR-009).
**Scale/Scope**: 5 concrete screens; up to 9 menu options per screen; 20 entities

---

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I — Clarity | ✅ Pass | `IScreen`, `ScreenAction`, `MenuOption`, `ConsoleRenderer` are self-describing; screen names map directly to player-visible concepts |
| II — Entity-Driven | ✅ Pass | Screens read entity data through `EntityController`; no screen touches entity internals. `Influence` is a data-only object on `Entity` |
| III — Incremental Playability | ✅ Pass | `Program.cs` replaces its current direct tick loop with `ScreenManager.Run()`. Game remains runnable at every step |
| IV — Test by Playing | ✅ Pass | 10 manual play-test scenarios in quickstart cover happy path and edge cases for every screen |
| V — Simplicity First | ✅ Pass | Full clear-and-redraw, numbered options, no external UI library, no color/animation. Screen stack is justified by the explicit requirement for context-sensitive multi-screen navigation |

No constitution violations. Complexity Tracking table not required.

---

## Project Structure

### Documentation (this feature)

```text
specs/001-entity-influence-engine/
├── plan.md              ← this file
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output
├── quickstart.md        ← Phase 1 output (updated, 10 scenarios)
├── contracts/
│   ├── item-definition-schema.md    ← item system (unchanged)
│   ├── effect-registry-api.md       ← item system (unchanged)
│   ├── location-system-api.md       ← location system (unchanged)
│   └── ui-screen-api.md             ← NEW
└── tasks.md             ← Phase 2 output (speckit.tasks)
```

### Source Code

```text
EntityObjects/                           ← domain library
├── Influence.cs                         ← NEW: TargetActionName, Strength, Source
├── Entity.cs                            ← UPDATE: add PendingInfluence property
└── ActionCandidate.cs                   ← UPDATE: add PlayerInfluence to ActionWeightSource enum

EntityARestaurantGame/                   ← console app
├── UI/                                  ← NEW directory
│   ├── IScreen.cs                       ← NEW: interface + ScreenAction hierarchy
│   ├── ScreenManager.cs                 ← NEW: stack + run loop
│   ├── ConsoleRenderer.cs               ← NEW: all Console.* calls live here
│   ├── MenuOption.cs                    ← NEW: label, enabled flag, execute func
│   └── Screens/                         ← NEW directory
│       ├── WorldScreen.cs               ← NEW: entity list, tick control
│       ├── EntityListScreen.cs          ← NEW: pick entity (inspect or influence mode)
│       ├── EntityInspectScreen.cs       ← NEW: behaviors, relationships, decision log
│       ├── InfluenceScreen.cs           ← NEW: pick action to nudge toward
│       └── InfluenceResultScreen.cs     ← NEW: show accepted/rejected + reason
├── EntityController.cs                  ← UPDATE: consume PendingInfluence in ProcessTick
└── Program.cs                           ← UPDATE: replace tick loop with ScreenManager.Run()
```

---

## Implementation Sequence

1. `Influence.cs` + `ActionWeightSource.PlayerInfluence` + `Entity.PendingInfluence`
2. `EntityController.ProcessTick()` — inject `PendingInfluence` as `ActionWeight`, clear after tick
3. `IScreen.cs` — interface + `ScreenAction` sealed class hierarchy
4. `MenuOption.cs`
5. `ConsoleRenderer.cs` — `Clear`, `DrawStatusBar`, `DrawPanel`, `DrawOptions`, `Write`
6. `ScreenManager.cs` — stack + `Run()` loop
7. `WorldScreen.cs`
8. `EntityListScreen.cs`
9. `EntityInspectScreen.cs`
10. `InfluenceScreen.cs`
11. `InfluenceResultScreen.cs`
12. `Program.cs` — wire `ScreenManager` + initial entities; remove old tick loop

---

## RPG Maker Screen Model: How It Maps Here

| RPG Maker concept | This implementation |
|-------------------|---------------------|
| Scene/Scene_Map | `WorldScreen` — the "overworld" |
| Scene_Menu | `EntityListScreen` |
| Window_Command | `MenuOption` list rendered by `ConsoleRenderer.DrawOptions` |
| Window_Status | `EntityInspectScreen` |
| Scene stack (SceneManager) | `ScreenManager` stack |
| Conditional branch in events | `IsEnabled` on `MenuOption`; options rebuilt each render |
| Common Event | `InfluenceResultScreen` — a transient overlay result screen |
