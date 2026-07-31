# Research: Console UI System
**Feature**: 001-entity-influence-engine
**Phase**: 0 — Pre-Design Research

---

## Decision 1: UI Architecture Pattern

**Decision**: Screen stack (push/pop), each screen owns its render and input handling.

**Rationale**: This is precisely how RPG Maker structures its UI — a stack of "windows" where
opening a sub-menu pushes a new screen, and cancelling pops back to the previous one. It gives
context-sensitive controls for free: the active screen on top of the stack determines what the
player can do. The stack also makes it trivial to chain screens (World → EntityList →
EntityInspect → InfluenceMenu → Result) and unwind them individually.

**Alternatives considered**:
- Flat state machine with explicit transitions — equally valid but requires enumerating every
  possible transition upfront. The stack is more composable and requires less pre-planning.
- Single screen with mode flags — gets messy fast; conditional rendering scattered everywhere.

---

## Decision 2: Input Method

**Decision**: `Console.ReadKey(intercept: true)` for single-keystroke input. Numbered options
(1, 2, 3…) for menu selection; dedicated keys (Enter = confirm, Escape = back, Space = advance
turn) for navigation.

**Rationale**: Numbered options require no cursor tracking state and are instantly readable in a
console context. `intercept: true` suppresses echoing the raw keypress. Escape = pop screen
mirrors every RPG and console menu convention the player already knows.

**Alternatives considered**:
- Arrow key cursor with highlighted selection — requires tracking a cursor index per screen and
  re-rendering on every keypress. Adds complexity with no functional gain over numbered options
  at this stage.
- Line-input (`Console.ReadLine`) — forces the player to press Enter after every action; sluggish
  for menu navigation.

---

## Decision 3: Rendering

**Decision**: Full clear-and-redraw (`Console.Clear()`) on every input event. ASCII box-drawing
characters (`┌ ─ ┐ │ └ ┘`) for panel borders.

**Rationale**: Simplest correct approach — no partial-update complexity, no cursor tracking for
dirty regions. At < 20 entities the full redraw is imperceptible. Box-drawing characters are
in the standard Unicode set, supported by Windows Terminal and modern PowerShell. If flicker
appears, a single `Console.SetCursorPosition(0,0)` + overwrite can replace `Console.Clear()`
later with no structural change.

**Alternatives considered**:
- Spectre.Console — polished output but adds an external dependency. Ruled out by Principle V
  and the constitution's dependency justification requirement.
- ANSI escape codes directly — cross-platform but fragile; Console.Clear is more portable.

---

## Decision 4: Screen Ownership of Options

**Decision**: Each screen constructs its own `List<MenuOption>` based on current game state at
render time. Options are not stored globally.

**Rationale**: This is the "conditions" mechanic the user described — a screen inspecting a
dead entity shows different options than one inspecting a healthy entity. Rebuilding options on
every render keeps the logic local and avoids stale option lists. The cost is negligible (small
list construction per keypress).

**Alternatives considered**:
- Static option lists configured at screen construction — can't react to runtime state changes.
- Event-driven option registration — over-engineered for the current scale.

---

## Decision 5: Decoupling from Domain

**Decision**: Screens access game state exclusively through `EntityController` (already exists).
No screen reaches into `Entity` internals directly — it reads through controller properties
(`ActiveEntities`, `ProcessTick()`).

**Rationale**: FR-009 requires the core engine to be decoupled from console rendering. Screens
are in the console project; `EntityController` is the boundary. This also means the UI is
trivially replaceable (swap `Program.cs` and the `UI/` folder; the library is untouched).

---

## Decision 6: Turn Advancement

**Decision**: The player explicitly advances each turn with Space (or a "Next Turn" menu option).
The tick does not auto-advance.

**Rationale**: FR-010 requires the player to trigger each turn manually. This is also the
correct design for a step-by-step influence game — the player observes, decides, influences,
then advances. Auto-advancing would undermine the core mechanic.
