<!--
SYNC IMPACT REPORT
==================
Version change: (none) → 1.0.0 (initial ratification)

Modified principles: N/A — first-time population of all placeholders.

Added sections:
  - Core Principles (I–V)
  - Development Standards
  - Code Quality & Learning Standards
  - Governance

Removed sections: N/A

Templates requiring updates:
  ✅ .specify/templates/plan-template.md — Constitution Check section references game-specific gates
  ✅ .specify/templates/spec-template.md — no structural changes needed; principles are compatible
  ✅ .specify/templates/tasks-template.md — no structural changes needed; task categories align
  ✅ .specify/templates/checklist-template.md — no structural changes needed

Follow-up TODOs:
  - TODO(TECH_STACK): Confirm primary language/engine (C#/Unity, Godot, custom?) once project
    scaffolding begins. Update Section 2 accordingly.
-->

# EntityRestaurantGame Constitution

## Core Principles

### I. Clarity Over Cleverness

All code MUST prioritize readability and educational value above brevity or
elegance. Variable names, method names, and class names MUST clearly communicate
intent without requiring comments to decode them. Every non-obvious design
decision (e.g., a specific data layout for cache performance, a non-standard
pattern) MUST include an inline comment explaining the "why", not just the "what".
Abstractions MUST NOT be introduced unless they reduce duplication across at least
three concrete use sites.

**Rationale**: This is a learning-oriented game project. Future developers (or
the current developer revisiting the code) MUST be able to understand the system
by reading the code, not by asking the original author.

### II. Entity-Driven Architecture

Game objects MUST be modeled as entities composed of discrete, data-only
components. Behavior MUST live in systems that query and transform entity data.
Direct coupling between entity types (e.g., a `Customer` reaching into a `Table`
to read its state) is prohibited; all cross-entity communication MUST flow through
a shared data layer or event bus. Each component type MUST have a single, clearly
named responsibility.

**Rationale**: Entity-Component-System (ECS) and similar entity-driven patterns
teach clean separation of data and behavior, are highly testable, and scale well
as the game grows in complexity. The "Restaurant Game" domain (customers, tables,
orders, staff) maps naturally to discrete entities.

### III. Incremental Playability

Every merged feature MUST leave the game in a runnable, playable (or at minimum
launchable) state. "Dark work" phases where the game is broken for more than one
working session are prohibited. Features MUST be sliced vertically — a thin,
working slice is always preferred over a deep, broken one. If a full feature
cannot be completed in one pass, a feature flag or stub MUST be used to keep the
main branch stable.

**Rationale**: Continuously runnable code provides fast feedback, maintains
motivation, and makes it easy to demonstrate progress. It also enforces
disciplined feature slicing.

### IV. Test by Playing

Core game-logic systems (scoring, order fulfillment, state transitions) MUST have
automated unit tests that cover the primary happy path and at least two edge
cases. Gameplay loops and user-facing scenarios MUST be documented as manual
play-test scenarios (in `specs/[feature]/quickstart.md`) so that any contributor
can verify behavior without prior context. Automated tests MUST NOT mock the
entity/component layer; they MUST operate on real in-memory entity graphs.

**Rationale**: Game logic is subtle and easy to break silently. Documented
play-test scenarios serve as living specifications and make the system learnable
by doing, not just by reading.

### V. Simplicity First (YAGNI)

The simplest data structure or algorithm that satisfies the current requirement
MUST be chosen. Generalization, configuration systems, plugin architectures, and
performance micro-optimizations MUST NOT be added unless a concrete, present need
justifies them. When performance work is required, it MUST be preceded by a
profiling step that identifies the actual bottleneck — no speculative optimization.
Target frame budget: 60 fps; any system that cannot meet this in a measured test
MUST be flagged before merge.

**Rationale**: Over-engineering is the primary source of abandoned learning
projects. Keeping the codebase lean ensures the game stays playable and the
codebase stays approachable.

## Development Standards

TODO(TECH_STACK): Confirm primary language and engine before filling this section
fully. Placeholder values are provided below based on the "EntityRestaurantGame"
project name and Windows development environment.

- **Language**: C# (assumed; update if using GDScript/Rust/other)
- **Engine/Framework**: Unity or Godot (update once confirmed)
- **Version Control**: Git, branch-per-feature, PRs required for all changes
- **Code Style**: Follow engine-standard conventions (e.g., C# PascalCase for
  public members, camelCase for private fields with underscore prefix `_field`)
- **Directory Layout**:
  - `src/` or `Assets/Scripts/` — all game logic, organized by domain
    (Entities/, Components/, Systems/, UI/)
  - `tests/` — unit tests mirroring `src/` structure
  - `specs/` — feature documentation produced by speckit workflow
- **Dependencies**: External packages MUST be justified in the relevant spec
  before being added; transitive dependencies MUST be audited for license
  compatibility

## Code Quality & Learning Standards

- Every public type and public method MUST have a summary comment (XML doc in C#,
  or equivalent) describing its purpose in one sentence.
- Magic numbers and magic strings are prohibited; use named constants or enums.
- A new contributor MUST be able to run the game from a clean checkout in under
  5 minutes by following `docs/quickstart.md` (to be created at project start).
- Code reviews MUST verify that Principle I (Clarity) is satisfied — reviewers
  SHOULD ask "would a junior developer understand this without asking?" before
  approving.
- Commit messages MUST use conventional-commit format:
  `type(scope): short description` (e.g., `feat(orders): add order queue system`)

## Governance

This constitution supersedes all other informal practices and verbal agreements
about code style or architecture. Amendments require:

1. A written proposal (can be a PR description or spec update) describing the
   change and its motivation.
2. Review and acknowledgement by the primary contributor(s).
3. An update to this file with an incremented version and today's date in
   `LAST_AMENDED_DATE`.
4. A migration note if existing code must be updated to comply.

All pull requests and code reviews MUST verify compliance with Principles I–V.
Additional complexity beyond what the constitution permits MUST be documented
in the plan's **Complexity Tracking** table with explicit justification.

Use `.specify/memory/constitution.md` (this file) as the authoritative runtime
reference during all speckit workflow steps.

**Version**: 1.0.0 | **Ratified**: 2026-03-19 | **Last Amended**: 2026-03-19
