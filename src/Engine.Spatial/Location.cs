using System.Numerics;

namespace Engine.Spatial;

/// <summary>
/// A named axis-aligned bounding box (AABB) in the game world.
/// Two Vector3 corners — Min and Max — define the region.
/// Axis convention: X = left/right, Y = front/back, Z = floor layer.
/// </summary>
public class Location
{
    public string Tag { get; }
    public Vector3 Min { get; }
    public Vector3 Max { get; }

    /// <summary>Volume of the bounding box — used to pick the most specific location when zones overlap.</summary>
    public float Volume { get; }

    public Location(string tag, Vector3 min, Vector3 max)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Location tag must not be empty.", nameof(tag));
        if (min.X > max.X)
            throw new ArgumentException($"Min.X ({min.X}) must be ≤ Max.X ({max.X}).", nameof(min));
        if (min.Y > max.Y)
            throw new ArgumentException($"Min.Y ({min.Y}) must be ≤ Max.Y ({max.Y}).", nameof(min));
        if (min.Z > max.Z)
            throw new ArgumentException($"Min.Z ({min.Z}) must be ≤ Max.Z ({max.Z}).", nameof(min));

        Tag = tag;
        Min = min;
        Max = max;
        Volume = (max.X - min.X) * (max.Y - min.Y) * (max.Z - min.Z);
    }

    /// <summary>Returns true if the point falls within this location's bounds (inclusive).</summary>
    public bool Contains(Vector3 point) =>
        point.X >= Min.X && point.X <= Max.X &&
        point.Y >= Min.Y && point.Y <= Max.Y &&
        point.Z >= Min.Z && point.Z <= Max.Z;
}
