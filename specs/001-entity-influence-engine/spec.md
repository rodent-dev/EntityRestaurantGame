# Feature Specification: Entity Influence Engine — Core Game Foundation

**Feature Branch**: `001-entity-influence-engine`
**Created**: 2026-03-19
**Status**: Draft
**Input**: User description: "We will be focusing on low level game development, we are responsible
for all processes and interactions in the game. We will originally implement a C# console application
focusing on the key principals of the application, and build a structure which can be reused and
moved to other application forms. The premise of the game is based on influencing entity decisions
as opposed to direct player commands. Each entity will have a set of relationships and behaviors,
and by combining those with world factors will decide their outcomes."

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Observing Entity Autonomous Behavior (Priority: P1)

A player starts a game session and watches entities (e.g., restaurant staff and customers) go about
their routines without any player input. Entities make decisions based on their own internal state,
their relationships with other entities, and the current state of the world. The player can see each
entity's current action and the reason behind it displayed in the console.

**Why this priority**: This is the core loop of the game. Without autonomous entity behavior, there
is nothing for the player to influence. Every other story depends on this working first.

**Independent Test**: Can be tested by launching the game, providing no input, and observing that
entities cycle through at least three distinct behaviors over time without any player action.
Delivers the "world lives on its own" value independently.

**Acceptance Scenarios**:

1. **Given** a new game session has started, **When** the player takes no action for 10 simulated
   turns, **Then** each entity has performed at least one autonomous decision, displayed with its
   triggering reason (relationship, world factor, or behavior rule).
2. **Given** two entities share a relationship (e.g., coworkers), **When** a world factor changes
   (e.g., restaurant becomes busy), **Then** both entities update their behavior in a way consistent
   with their relationship and the new world state.
3. **Given** an entity has conflicting behavioral signals (e.g., hungry but also mid-task),
   **When** the decision cycle runs, **Then** the entity resolves to one action via a defined
   priority rule, and that resolution is visible to the player.

---

### User Story 2 - Player Influencing an Entity Decision (Priority: P2)

The player selects an entity and applies an influence (e.g., encouragement, a hint, environmental
change) that nudges that entity's next decision without directly commanding it. The entity weighs
the influence against its existing relationships, behaviors, and world factors, and may or may not
follow the suggested direction.

**Why this priority**: This is the distinguishing mechanic of the game — influence rather than
command. It is the primary form of player agency.

**Independent Test**: Can be tested by selecting any entity, applying an influence action, advancing
one decision cycle, and verifying that the entity's decision reflects the influence (either accepted
or rejected with a visible reason). Delivers player agency independently from story completeness.

**Acceptance Scenarios**:

1. **Given** an entity is in an undecided or idle state, **When** the player applies a positive
   influence, **Then** the entity's next decision is at least 50% more likely to align with the
   influence direction, and this probability shift is visible in the output.
2. **Given** an entity has a strong contrary relationship (e.g., dislikes the target task),
   **When** the player applies an influence, **Then** the entity may reject the influence, and the
   reason for rejection references the conflicting relationship.
3. **Given** the player applies an influence that contradicts a world factor (e.g., rush customer
   when kitchen is at capacity), **When** the entity evaluates the influence, **Then** the world
   factor weight reduces the influence effectiveness and the entity's displayed reasoning reflects
   this.

---

### User Story 3 - Inspecting Entity State and Relationships (Priority: P3)

The player can select any entity and view a summary of its current state: active behaviors, key
relationships, relevant world factors, and a short log of recent decisions. This allows the player
to understand why entities behave as they do and to plan future influences.

**Why this priority**: Without visibility into entity internals, the player cannot make informed
influence decisions. This is required for meaningful gameplay but depends on Stories 1 and 2
working first.

**Independent Test**: Can be tested by selecting any entity and verifying that the displayed
summary includes at least one behavior, one relationship, one world factor, and the last three
decisions with reasons.

**Acceptance Scenarios**:

1. **Given** the player selects an entity, **When** the inspect action is triggered, **Then** the
   console displays the entity's name, current behavior, all active relationships with strength
   values, all active world factors affecting it, and the last three decisions.
2. **Given** a relationship between two entities changes strength during play, **When** the player
   inspects either entity, **Then** the updated relationship strength is reflected immediately.
3. **Given** a world factor is added or removed, **When** the player inspects any affected entity,
   **Then** the factor appears or disappears from that entity's world-factor list.

---

### Edge Cases

- What happens when an entity has no active relationships? The entity falls back to behavior-only
  decisions; this isolated state is visible in the entity's display.
- What happens when all world factors are neutral? Entities rely solely on relationship weights and
  base behaviors; no crash or undefined state should occur.
- What happens when the player attempts to influence an entity that is mid-decision? The influence
  is queued for the next decision cycle and the player is notified.
- What happens when two player influences conflict on the same entity in one turn? The most recently
  applied influence takes precedence; ties are resolved by influence strength.
- What happens when an entity's decision history is empty (brand-new entity)? The inspect view
  shows "No decisions yet" rather than an empty or error state.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST model each entity with a set of behaviors (discrete action tendencies
  with weights), a set of directional relationships to other entities, and sensitivity to world
  factors.
- **FR-002**: The system MUST run an autonomous decision cycle each game turn that evaluates every
  entity's behaviors, relationships, and active world factors to produce a chosen action.
- **FR-003**: Each entity's chosen action and the primary reason for that choice MUST be displayed
  to the player each turn.
- **FR-004**: The player MUST be able to apply an influence to a specific entity that adjusts the
  probability weighting of that entity's next decision without overriding it directly.
- **FR-005**: The system MUST communicate whether an applied influence was accepted, partially
  accepted, or rejected by the entity, along with the reason.
- **FR-006**: The player MUST be able to inspect any entity to view its current behaviors,
  relationships, world-factor sensitivities, and recent decision history (last three decisions).
- **FR-007**: World factors MUST be representable as named conditions with a magnitude value that
  any relevant entity can read during its decision cycle.
- **FR-008**: Relationships MUST be directional (Entity A's view of Entity B may differ from
  Entity B's view of Entity A) and MUST carry a numerical strength value.
- **FR-009**: The core entity model, decision engine, and influence system MUST be implemented
  as components decoupled from the console rendering layer so they can be reused in other
  application forms without modification.
- **FR-010**: The system MUST advance game state turn by turn; the player MUST be able to trigger
  each turn manually to support step-by-step observation for learning and debugging.
- **FR-011**: Entity definitions (behaviors, relationships, world-factor sensitivities) MUST be
  loadable from external data so new entity types can be added without changing existing code.

### Key Entities

- **Entity**: A named actor in the game world (e.g., a restaurant worker or customer). Has an
  identity, a collection of behaviors, a set of outgoing relationships, and a decision history.
- **Behavior**: A named action tendency with a base weight describing how strongly the entity
  defaults to that action pattern (e.g., "serve customers: weight 0.8").
- **Relationship**: A directional link from one entity to another with a numerical strength value
  and a valence (positive/negative/neutral). Influences decision weighting when the target entity
  or their situation is relevant.
- **WorldFactor**: A named environmental condition with a magnitude value visible to all or a
  defined subset of entities (e.g., "restaurant occupancy: 0.9", "kitchen stress: 0.7").
- **Decision**: A record of one entity's chosen action for one turn, including the action taken,
  the primary contributing factors (behavior, relationship, world factor), and any player influence
  that was applied.
- **Influence**: A player-initiated nudge targeting a specific entity, expressing a preferred
  direction with a strength value. Consumed during the entity's next decision cycle.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new contributor can run a full game session from a clean checkout and observe
  autonomous entity decisions within 5 minutes by following the quickstart guide.
- **SC-002**: Every entity decision displays the top contributing factor (behavior, relationship,
  or world factor) so the player can always understand why an entity acted as it did.
- **SC-003**: Player influences visibly affect entity decision outcomes in at least 70% of cases
  where no strong contrary factor exists, confirming the influence mechanic is functional.
- **SC-004**: The core decision engine can be exercised in a non-console context (e.g., a unit
  test or alternative host) without referencing any console-specific code, confirming architectural
  portability.
- **SC-005**: All entity behaviors, relationships, and world factors can be defined via data
  (not hard-coded logic), allowing a new entity type to be added without changing existing code.
- **SC-006**: The game processes and displays each turn's results within 1 second for a world
  containing up to 20 entities, maintaining interactive responsiveness.

---

## Assumptions

- The initial game scenario is a restaurant setting (confirmed by project name
  "EntityRestaurantGame"), but the engine itself is designed to be domain-agnostic.
- Turn-based progression (not real-time) is assumed for the console prototype, enabling step-by-step
  observation suitable for learning and debugging.
- A single player interacts with the game; multiplayer is out of scope for this foundational
  feature.
- Entity data (behaviors, relationships, world factors) will be defined in external structured data
  files rather than hard-coded; the exact format is a planning-phase decision.
- The console output is text-only for this phase; visual or graphical rendering is explicitly
  deferred to a future application-form migration.
- "Influence" is a deliberate, per-entity action the player takes each turn; passive environmental
  changes driven by world factors are handled automatically by the engine, not by direct commands.
