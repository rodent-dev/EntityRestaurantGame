# Contract: Item Definition JSON Schema

All item files under `data/items/` MUST conform to this schema.
One JSON object per file.

---

## Full Schema

```json
{
  "itemId":             "string (required, unique)",
  "itemName":           "string (required)",
  "description":        "string (required)",
  "type":               "Standard | Weapon | Consumable | Merit | Skill (required)",
  "weight":             "float (required, >= 0)",
  "uses":               "int (required, -1 = infinite, > 0 = limited)",
  "isEquipped":         "bool (optional, default: false)",
  "speakEffectModifier":"float (optional, default: 0.0)",
  "enablesActions":     ["string", "..."],
  "effects":            [EffectDefinition, "..."],
  "handlerClass":       "string? (optional, fully-qualified C# type name)"
}
```

---

## EffectDefinition Schema

```json
{
  "type":       "string (required) — registered keyword",
  "trigger":    "string (required) — action name",
  "parameters": { "key": 0.0 }
}
```

---

## Registered Effect Keywords

| `type` | Required `parameters` keys | Optional keys |
|--------|---------------------------|---------------|
| `Damage` | `amount` | — |
| `Heal` | `amount` | — |
| `Relationship` | `amount` | — |
| `ConsumeItem` | — | — |
| `StatModify` | `amount` | `duration` (-1 = permanent, default) |

> `StatModify` also requires a top-level `"statName"` string property on the effect object (not inside parameters). See extended schema below.

---

## StatModify Extended Schema

```json
{
  "type":       "StatModify",
  "trigger":    "string",
  "statName":   "string (e.g. \"strength\", \"agility\")",
  "parameters": {
    "amount":   0.0,
    "duration": -1.0
  }
}
```

---

## Examples

### Fist (infinite-use weapon)
```json
{
  "itemId": "fist",
  "itemName": "Fist",
  "description": "A bare-knuckled fist.",
  "type": "Weapon",
  "weight": 0.0,
  "uses": -1,
  "isEquipped": true,
  "speakEffectModifier": 0.0,
  "enablesActions": ["Attack", "Defend"],
  "effects": [
    {
      "type": "Damage",
      "trigger": "Attack",
      "parameters": { "amount": 5.0 }
    }
  ]
}
```

### Gift (single-use merit item)
```json
{
  "itemId": "gift",
  "itemName": "A Small Gift",
  "description": "A nicely wrapped gift.",
  "type": "Merit",
  "weight": 1.0,
  "uses": 1,
  "speakEffectModifier": 1.0,
  "enablesActions": ["Speak"],
  "effects": [
    {
      "type": "Relationship",
      "trigger": "Speak",
      "parameters": { "amount": 10.0 }
    },
    {
      "type": "ConsumeItem",
      "trigger": "Speak",
      "parameters": {}
    }
  ]
}
```

### Health Potion (consumable, self-heal)
```json
{
  "itemId": "health_potion",
  "itemName": "Health Potion",
  "description": "Restores a modest amount of health.",
  "type": "Consumable",
  "weight": 0.5,
  "uses": 1,
  "enablesActions": ["Defend"],
  "effects": [
    {
      "type": "Heal",
      "trigger": "Defend",
      "parameters": { "amount": 30.0 }
    },
    {
      "type": "ConsumeItem",
      "trigger": "Defend",
      "parameters": {}
    }
  ]
}
```

### Cursed Blade (handler class — custom behavior)
```json
{
  "itemId": "cursed_blade",
  "itemName": "Cursed Blade",
  "description": "Deals damage but also harms the wielder.",
  "type": "Weapon",
  "weight": 4.0,
  "uses": -1,
  "enablesActions": ["Attack"],
  "effects": [],
  "handlerClass": "EntityObjects.Items.CursedBlade"
}
```

### Digging Skill (Skill type — optional action enhancer for Scavenge)
```json
{
  "itemId": "digging_skill",
  "itemName": "Digging Skill",
  "description": "An innate knack for finding buried things.",
  "type": "Skill",
  "weight": 0.0,
  "uses": -1,
  "isEquipped": true,
  "enablesActions": ["Scavenge"],
  "effects": [
    {
      "type": "StatModify",
      "trigger": "Scavenge",
      "statName": "intelligence",
      "parameters": { "amount": 5.0, "duration": -1.0 }
    }
  ]
}
```

> **Skill items** are inherent abilities, not possessions. They are: `type: "Skill"`,
> `uses: -1` (infinite), `isEquipped: true`, and non-transferable by convention.
> The action they enable does not require them — they act as optional enhancers.

---

## Item Type Reference

| Type | Description | Transferable | Typical Uses |
|------|-------------|--------------|--------------|
| `Weapon` | Physical implements — enable Attack/Defend/Grapple | Yes | `>= 1` or `-1` |
| `Consumable` | Food, drink, potions — enable Consume | Yes | `>= 1` |
| `Merit` | Social goods — enable Speak | Yes | `1` (gifted) |
| `Standard` | Miscellaneous items | Yes | varies |
| `Skill` | Innate abilities — optional enhancers | No | `-1` |

---

## Validation Rules

- `itemId` MUST be unique across all loaded item files.
- `enablesActions` MUST NOT be empty (an item that enables no actions is inert and pointless).
- Each effect `type` MUST be registered in `EffectRegistry` at load time; unregistered types throw at startup, not at runtime.
- `handlerClass` type MUST exist and MUST have a constructor accepting `ItemDefinition`; missing type throws at startup.
- `uses` value of `0` is invalid (depleted on creation); use `-1` for infinite or `>= 1` for limited.
