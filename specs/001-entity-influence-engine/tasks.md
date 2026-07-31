# Tasks: Entity Influence Engine — Full Implementation
**Feature**: `001-entity-influence-engine` | **Generated**: 2026-03-19

Covers three planned systems in dependency order:
- **Phase A** — Location System
- **Phase B** — JSON Item System
- **Phase C** — UI System (domain additions + screens)
- **Phase D** — Wire-up & validation

---

## Phase A — Location System

- [X] **A-01** — Create `EntityObjects/Spatial/Location.cs` — Tag, Min, Max (Vector3), Volume, Contains()
- [X] **A-02** — Create `EntityObjects/Spatial/LocationRegistry.cs` — Register, GetByTag, FindLocation, FindAllLocations
- [X] **A-03** — Update `EntityObjects/Entity.cs` — add `Vector3 Position { get; set; } = Vector3.Zero`

---

## Phase B — JSON Item System

- [X] **B-01** — Create `EntityObjects/Effects/IActionEffect.cs` — `void Apply(Entity, Entity, Dictionary<string,float>)`
- [X] **B-02** — Create `EntityObjects/Effects/EffectRegistry.cs` — Register, Resolve, IsRegistered
- [X] **B-03** [P] — Create `EntityObjects/Effects/DamageEffect.cs`
- [X] **B-04** [P] — Create `EntityObjects/Effects/HealEffect.cs`
- [X] **B-05** [P] — Create `EntityObjects/Effects/RelationshipEffect.cs`
- [X] **B-06** [P] — Create `EntityObjects/Effects/ConsumeItemEffect.cs`
- [X] **B-07** — Create `EntityObjects/Definitions/ItemDefinition.cs` — JSON POCO matching schema
- [X] **B-08** — Create `EntityObjects/Items/DataDrivenItem.cs` — IItem backed by ItemDefinition + EffectRegistry
- [X] **B-09** — Create `EntityObjects/Items/ItemFactory.cs` — LoadFromFile, LoadDirectory, Create (handler/data routing)
- [X] **B-10** [P] — Create `data/items/fist.json`
- [X] **B-11** [P] — Create `data/items/gift.json`
- [X] **B-12** — Update `EntityARestaurantGame.csproj` — add `<None>` copy rule for `data/` directory

---

## Phase C — UI Domain Additions

- [X] **C-01** — Update `EntityObjects/ActionCandidate.cs` — add `PlayerInfluence` to `ActionWeightSource` enum
- [X] **C-02** — Create `EntityObjects/Influence.cs` — TargetActionName, Strength, Source
- [X] **C-03** — Update `EntityObjects/Entity.cs` — add `Influence? PendingInfluence { get; set; }`
- [X] **C-04** — Update `EntityARestaurantGame/EntityController.cs` — inject PendingInfluence as ActionWeight in ProcessTick, clear after tick; record last decision per entity

---

## Phase D — UI Layer

- [X] **D-01** — Create `EntityARestaurantGame/UI/IScreen.cs` — IScreen interface + ScreenAction sealed hierarchy (Stay, Push, Pop, Replace, Quit)
- [X] **D-02** — Create `EntityARestaurantGame/UI/MenuOption.cs`
- [X] **D-03** — Create `EntityARestaurantGame/UI/ConsoleRenderer.cs` — Clear, DrawStatusBar, DrawPanel, DrawOptions, Write
- [X] **D-04** — Create `EntityARestaurantGame/UI/ScreenManager.cs` — stack + Run() loop
- [X] **D-05** — Create `EntityARestaurantGame/UI/Screens/WorldScreen.cs`
- [X] **D-06** — Create `EntityARestaurantGame/UI/Screens/EntityListScreen.cs`
- [X] **D-07** — Create `EntityARestaurantGame/UI/Screens/EntityInspectScreen.cs`
- [X] **D-08** — Create `EntityARestaurantGame/UI/Screens/InfluenceScreen.cs`
- [X] **D-09** — Create `EntityARestaurantGame/UI/Screens/InfluenceResultScreen.cs`
- [X] **D-10** — Update `EntityARestaurantGame/Program.cs` — register effects, build registry, wire ScreenManager, seed entities with positions

---

## Dependency Order

```
A-01 → A-02 (registry needs Location type)
A-01, A-02 → A-03 (independent; group for clarity)
B-01 → B-02 → B-03..B-06 [parallel] → B-07 → B-08 → B-09 → B-10, B-11 [parallel] → B-12
C-01 → C-02 → C-03 → C-04
D-01 → D-02 → D-03 → D-04 → D-05 → D-06 → D-07 → D-08 → D-09 → D-10
Phase A, B, C must complete before D-10
```
