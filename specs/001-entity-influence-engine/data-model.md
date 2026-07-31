# Data Model: Console UI System
**Feature**: 001-entity-influence-engine

---

## Core Abstractions

### IScreen

Every interactive view implements this interface. The screen stack in `ScreenManager` holds a
sequence of `IScreen` instances; the topmost is always active.

| Member | Signature | Responsibility |
|--------|-----------|----------------|
| `Title` | `string` | Displayed in the panel header |
| `Render` | `(ConsoleRenderer r) → void` | Draws the full screen content |
| `HandleInput` | `(ConsoleKeyInfo key) → ScreenAction` | Processes one keypress; returns what to do next |

`HandleInput` never blocks — it receives a single keypress that was already read by
`ScreenManager` and returns a `ScreenAction` value:

```
ScreenAction (discriminated union / sealed class hierarchy)
  ├── Stay          — redraw current screen, no navigation
  ├── Push(IScreen) — push a new screen on top
  ├── Pop           — remove this screen, return to previous
  ├── Replace(IScreen) — swap this screen with another (no back)
  └── Quit          — exit the application
```

---

### ScreenManager

Owns the screen stack and the main game loop.

| Member | Responsibility |
|--------|----------------|
| `Push(IScreen)` | Adds a screen to the top of the stack |
| `Pop()` | Removes the top screen |
| `Run()` | Blocking loop: read key → dispatch to top screen → apply `ScreenAction` → redraw |

```
Run() loop:
  while stack not empty:
    stack.Peek().Render(renderer)
    key = Console.ReadKey(intercept: true)
    action = stack.Peek().HandleInput(key)
    apply action (push / pop / replace / quit)
```

---

### MenuOption

A single selectable item in a numbered list.

| Field | Type | Notes |
|-------|------|-------|
| `Label` | `string` | Displayed text, e.g. `"[1] Inspect Entity"` |
| `Description` | `string?` | Optional subtitle shown below label |
| `IsEnabled` | `bool` | Greyed-out if false; input for its number is ignored |
| `Execute` | `Func<ScreenAction>` | Called when the player selects this option |

The number prefix is assigned by position in the list — option 0 = key `1`, option 1 = key `2`,
up to 9. Screens with more than 9 options should be split.

---

### ConsoleRenderer

Thin wrapper around `System.Console` — centralises all write calls so screens never call
`Console.Write` directly. Makes the rendering contract testable and swappable.

| Method | Responsibility |
|--------|----------------|
| `Clear()` | `Console.Clear()` |
| `DrawPanel(title, lines)` | Draws a bordered box with a title and content lines |
| `DrawOptions(List<MenuOption>)` | Renders numbered option list at the bottom of the screen |
| `DrawStatusBar(string)` | Writes a fixed status line at the top |
| `Write(x, y, text)` | Absolute-position write |

Box characters used: `┌ ─ ┐ │ └ ┘ ├ ┤`

---

## Concrete Screens

### WorldScreen *(entry point)*

Shown at startup. Displays the current tick, all active entities with their last action and HP,
and the main action options.

**Options** (always available):
- `[1] Next Turn` — calls `EntityController.ProcessTick()`; stays on WorldScreen
- `[2] Inspect Entity` — pushes `EntityListScreen` in inspect mode
- `[3] Influence Entity` — pushes `EntityListScreen` in influence mode
- `[Q] Quit`

**Conditional options** (example of context-sensitive menus):
- `[4] View Last Turn Results` — only enabled after at least one tick has run

---

### EntityListScreen

Shows all active entities as a numbered list. Mode is set at construction (`Inspect` or
`Influence`) and determines what happens when the player selects an entity.

**In Inspect mode**: selecting an entity pushes `EntityInspectScreen`.
**In Influence mode**: selecting an entity pushes `InfluenceScreen`.

Options: one per active entity (dynamically generated) + `[Esc] Back`.

---

### EntityInspectScreen

Shows the full state of one entity. Satisfies FR-006.

**Sections rendered**:
- Name, type, HP bar
- Behaviors (name, type, affect weight)
- Relationships (target entity name, affinity score)
- Last 3 decisions (action taken + primary contributing factor)

**Options**:
- `[1] Influence This Entity` — replaces with `InfluenceScreen` for this entity
- `[Esc] Back`

---

### InfluenceScreen

Lets the player apply a nudge to one entity's next decision. Satisfies FR-004 and FR-005.

**Displays**: entity name, current behavior weights, available action types the player can nudge
toward.

**Options**: one per registered action type (e.g., `[1] Nudge toward Attack`, `[2] Nudge toward
Speak`) + `[Esc] Back`.

On selection: applies an `Influence` to the entity (stores a temporary weight boost for the next
tick), then pushes `InfluenceResultScreen`.

---

### InfluenceResultScreen

Shows the outcome of the applied influence. Satisfies FR-005.

**Displays**:
- The influence that was applied
- Whether it was accepted / partially accepted / rejected
- The reason (e.g., "rejected: strong contrary relationship with target")

**Options**: `[Enter] Continue` → pops back to WorldScreen (or EntityInspectScreen).

---

## Influence (new domain concept)

A temporary weight modifier applied to one entity's action candidates before the next tick.
Consumed and cleared after `ProcessTick()` runs.

| Field | Type | Notes |
|-------|------|-------|
| `TargetActionName` | `string` | The action type being nudged (e.g., `"Attack"`) |
| `Strength` | `int` | Weight added to matching candidates for one tick |
| `Source` | `string` | `"Player"` — distinguishes from behavior/relationship weights |

`Entity` gets a nullable `PendingInfluence` property. `EntityController.ProcessTick()` reads it,
injects it as an additional `ActionWeight` with source `ActionWeightSource.PlayerInfluence`,
then clears it.

---

## State Flow

```
App Start
  └── ScreenManager.Push(WorldScreen)
        │
        ├── [Space / Next Turn] → ProcessTick() → redraw WorldScreen
        │
        ├── [Inspect] → Push(EntityListScreen[Inspect])
        │     └── select entity → Push(EntityInspectScreen)
        │           └── [Esc] → Pop → EntityListScreen
        │           └── [Influence] → Replace(InfluenceScreen)
        │                 └── select action → Push(InfluenceResultScreen)
        │                       └── [Continue] → Pop → Pop → WorldScreen
        │
        └── [Influence] → Push(EntityListScreen[Influence])
              └── select entity → Push(InfluenceScreen)
                    └── select action → Push(InfluenceResultScreen)
                          └── [Continue] → Pop → Pop → WorldScreen
```

---

## Screen Conditions (context-sensitive options)

Screens evaluate game state at render time to decide which options are enabled. Examples:

| Screen | Condition | Effect |
|--------|-----------|--------|
| `WorldScreen` | `tick == 0` | "View Last Turn Results" is disabled |
| `EntityListScreen` | Entity HP ≤ 0 | Entity option labelled `[dead]`; IsEnabled = false |
| `InfluenceScreen` | Entity already has pending influence | Shows warning; re-applying replaces previous |
| `EntityInspectScreen` | Entity has no decisions yet | Decision history shows `"No decisions yet"` |
