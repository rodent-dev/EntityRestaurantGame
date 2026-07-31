# Quickstart: Entity Influence Engine

## Prerequisites

- .NET 10 SDK ([download](https://dotnet.microsoft.com/download/dotnet/10.0))
- A terminal that supports Unicode box-drawing characters (Windows Terminal recommended)

---

## Run in 60 seconds

```bash
git clone <repo>
cd EntityARestaurantGame
dotnet run --project EntityARestaurantGame
```

You will land on the **World Screen** showing your active entities.

---

## Controls

| Key | Action |
|-----|--------|
| `Space` | Advance one game turn |
| `1`–`9` | Select a numbered menu option |
| `Escape` | Go back to the previous screen |
| `Q` | Quit |

---

## Typical session flow

```
World Screen
  → observe entity states
  → [Space] advance a turn, watch decisions
  → [2] Inspect Entity → pick an entity → read behaviors + decision log
  → [Esc] back
  → [3] Influence Entity → pick entity → pick an action to nudge toward
  → view influence result → [Enter] return to World Screen
  → [Space] advance turn — observe whether the influence took effect
```

---

## How to define locations

```csharp
var registry = new LocationRegistry();
registry.Register(new Location("kitchen",      new Vector3(0,  0, 0), new Vector3(10, 8,  1)));
registry.Register(new Location("dining_floor", new Vector3(0,  8, 0), new Vector3(20, 20, 1)));
registry.Register(new Location("restaurant",   new Vector3(0,  0, 0), new Vector3(20, 20, 1)));
```

Axis convention: X = left/right, Y = front/back, Z = floor layer (0 = ground floor).

---

## How to place and query an entity

```csharp
chef.Position = new Vector3(5, 4, 0);
Location? current = registry.FindLocation(chef.Position);
// → "kitchen"
```

---

## How to add a new item (no code required)

Create a JSON file in `data/items/`:

```json
{
  "itemId": "my_item",
  "itemName": "My Item",
  "description": "Does something useful.",
  "type": "Weapon",
  "weight": 1.0,
  "uses": 3,
  "enablesActions": ["Attack"],
  "effects": [
    { "type": "Damage", "trigger": "Attack", "parameters": { "amount": 8.0 } }
  ]
}
```

---

## How to add a new screen

Implement `IScreen` and push it from any existing screen's `HandleInput`:

```csharp
public ScreenAction HandleInput(ConsoleKeyInfo key)
{
    if (key.KeyChar == '1')
        return ScreenAction.Push(new MyCustomScreen(_controller));
    if (key.Key == ConsoleKey.Escape)
        return ScreenAction.Pop;
    return ScreenAction.Stay;
}
```

---

## Manual play-test scenarios

### Scenario 1 — World screen renders on startup
1. Run the game.
2. **Pass**: World Screen title visible, at least one entity listed with HP and last action shown.

### Scenario 2 — Turn advances on Space
1. On World Screen, press Space.
2. **Pass**: Tick counter increments; entity states update; no crash.

### Scenario 3 — Inspect screen shows entity detail
1. Press `[2]`, select any entity.
2. **Pass**: Entity name, HP, at least one behavior, and decision history visible.

### Scenario 4 — Influence applies and result is shown
1. Press `[3]`, select an entity, select an action to nudge.
2. **Pass**: Influence result screen shows accepted/rejected + reason. Entity has `PendingInfluence` set.

### Scenario 5 — Influence is consumed on next turn
1. Apply influence (Scenario 4), return to World Screen, press Space.
2. **Pass**: `PendingInfluence` is null after tick; tick result reflects or rejects the nudge.

### Scenario 6 — Escape navigates back correctly
1. Open EntityList → EntityInspect → press Escape.
2. **Pass**: Returns to EntityList, not World Screen. Press Escape again → World Screen.

### Scenario 7 — Dead entity shows as disabled in list
1. Reduce an entity's HP to 0 (via several attack ticks).
2. Open EntityList (Inspect mode).
3. **Pass**: Dead entity is listed but greyed/labelled [dead]; selecting its number does nothing.

### Scenario 8 — Entity outside all locations returns null
1. Set `entity.Position = new Vector3(999, 999, 0)` (no location covers it).
2. Call `registry.FindLocation(entity.Position)`.
3. **Pass**: Returns `null`; no exception.

### Scenario 9 — JSON item loads correctly
1. Create `data/items/test_sword.json` with Damage 20.
2. Assign to warrior, advance 2 turns.
3. **Pass**: 20 damage per hit; item depletes if uses were limited.

### Scenario 10 — Unknown effect type fails at startup
1. Add `"type": "Teleport"` to any item's effects.
2. **Pass**: Clear startup error naming the unregistered keyword; no mid-tick crash.
