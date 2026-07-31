using System.Numerics;

namespace Engine.Spatial;

/// <summary>
/// Holds all registered locations and answers spatial queries.
/// Location lookup is a linear scan — suitable for fewer than 100 locations.
/// </summary>
public class LocationRegistry
{
    private readonly Dictionary<string, Location> _locations = new();

    public IReadOnlyList<Location> All => _locations.Values.ToList();

    /// <summary>Adds a location. Throws if the tag is already registered.</summary>
    public void Register(Location location)
    {
        if (_locations.ContainsKey(location.Tag))
            throw new ArgumentException($"A location with tag '{location.Tag}' is already registered.");
        _locations[location.Tag] = location;
    }

    /// <summary>Returns the location with the given tag, or null if not found.</summary>
    public Location? GetByTag(string tag) =>
        _locations.TryGetValue(tag, out var loc) ? loc : null;

    /// <summary>
    /// Returns the smallest (most specific) location containing the point.
    /// Returns null if no registered location contains the point.
    /// </summary>
    public Location? FindLocation(Vector3 position) =>
        FindAllLocations(position).FirstOrDefault();

    /// <summary>
    /// Returns all locations containing the point, ordered smallest-first (most specific first).
    /// </summary>
    public IEnumerable<Location> FindAllLocations(Vector3 position) =>
        _locations.Values
            .Where(loc => loc.Contains(position))
            .OrderBy(loc => loc.Volume);
}
