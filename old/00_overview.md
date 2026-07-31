# Game Design Specification
## Project Codename: *[TBD]*
**Stack:** C# — Raw (No Engine)
**Last Updated:** 2026-03-18
**Status:** Draft v0.1

---

## Purpose

This document is a living specification for a Rimworld-adjacent simulation game built in raw C#. The player acts as an *influencer* — not a direct controller — of a team of entities whose behaviors, relationships, and actions emerge from a weighted decision system.

This spec is organized into self-contained modules. Each module describes a system, its responsibilities, its key data structures, and its interface contracts with other systems.

---

## Module Index

| # | Module | Description |
|---|--------|-------------|
| 01 | [Entity System](./01_entity_system.md) | Core domain object, component attachment model |
| 02 | [Behavior Weighting Engine](./02_behavior_weighting.md) | How entities decide what to do |
| 03 | [Action Resolution System](./03_action_resolution.md) | What actions are, how they resolve |
| 04 | [Entity Control & Range System](./04_entity_control_range.md) | Who gets considered, when, and how far |

---

## Design Pillars

1. **Influence, not control.** The player nudges probabilities — entities make their own choices from those weighted options.
2. **Modularity over monolith.** Behaviors, physiques, stats, and actions are attachable components, not hardcoded properties.
3. **Determinism with texture.** The core simulation is deterministic and legible; a randomness layer adds unpredictability without making it feel arbitrary.
4. **Items as verbs.** Every action requires an attached item. Items are not just consumables — they are intent carriers (a trophy enables a persuasion attempt; a weapon enables an attack).
