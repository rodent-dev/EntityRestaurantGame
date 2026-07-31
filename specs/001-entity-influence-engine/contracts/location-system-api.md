# Contract: Location System API

---

## Location

```csharp
// Immutable. Constructed with two corner points.
public class Location
{
    public string Tag { get; }
    public Vector3 Min { get; }
    public Vector3 Max { get; }
    public float Volume { get; }            // (Max - Min) component product

    public Location(string tag, Vector3 min, Vector3 max);  // throws if min > max on any axis

    public bool Contains(Vector3 point);    // inclusive bounds check on all three axes
}
```

---

## LocationRegistry

```csharp
public class LocationRegistry
{
    public IReadOnlyList<Location> All { get; }

    // Throws ArgumentException if tag is already registered.
    public void Register(Location location);

    // Returns null if tag not found.
    public Location? GetByTag(string tag);

    // Returns the smallest (lowest volume) Location containing the point.
    // Returns null if no registered location contains the point.
    public Location? FindLocation(Vector3 position);

    // Returns all locations containing the point, ordered smallest-first.
    public IEnumerable<Location> FindAllLocations(Vector3 position);
}
```

---

## Entity.Position

```csharp
// Added to existing Entity class
public Vector3 Position { get; set; }   // default: Vector3.Zero
```

---

## Usage pattern

```csharp
// Setup
var registry = new LocationRegistry();
registry.Register(new Location("kitchen",      new Vector3(0, 0, 0),   new Vector3(10, 8, 1)));
registry.Register(new Location("dining_floor", new Vector3(0, 8, 0),   new Vector3(20, 20, 1)));
registry.Register(new Location("restaurant",   new Vector3(0, 0, 0),   new Vector3(20, 20, 1)));

// Entity placement
chef.Position = new Vector3(5, 4, 0);

// Query
Location? current = registry.FindLocation(chef.Position);
// → returns "kitchen" (smallest containing location)

IEnumerable<Location> all = registry.FindAllLocations(chef.Position);
// → ["kitchen", "restaurant"] ordered by volume ascending
```

---

## Error conditions

| Scenario | Behaviour |
|----------|-----------|
| `new Location(tag, min, max)` where any `min` component > matching `max` | Throws `ArgumentException` with axis named |
| `Register` with duplicate tag | Throws `ArgumentException` with tag named |
| `GetByTag` with unknown tag | Returns `null` |
| `FindLocation` for a point in no location | Returns `null` |
| Entity at `Vector3.Zero` with no location registered | Returns `null` (not an error) |
