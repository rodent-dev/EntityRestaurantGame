using System.Numerics;

namespace Engine.Render;

/// <summary>
/// Yaw/pitch/distance camera orbiting a target point on the XZ plane.
/// Feed it input deltas; ask it for a view matrix.
/// </summary>
public sealed class OrbitCamera
{
    public Vector3 Target { get; set; }
    public float Yaw { get; set; }                  // radians around +Y
    public float Pitch { get; set; } = 0.9f;        // radians above horizon
    public float Distance { get; set; } = 18f;

    public float MinPitch { get; set; } = 0.15f;
    public float MaxPitch { get; set; } = 1.45f;
    public float MinDistance { get; set; } = 3f;
    public float MaxDistance { get; set; } = 60f;

    public Vector3 Eye
    {
        get
        {
            float horiz = Distance * MathF.Cos(Pitch);
            return Target + new Vector3(
                horiz * MathF.Sin(Yaw),
                Distance * MathF.Sin(Pitch),
                horiz * MathF.Cos(Yaw));
        }
    }

    public void Orbit(Vector2 delta)
    {
        Yaw -= delta.X * 0.008f;
        Pitch = Math.Clamp(Pitch + delta.Y * 0.008f, MinPitch, MaxPitch);
    }

    public void Zoom(float scroll) =>
        Distance = Math.Clamp(Distance * MathF.Pow(0.9f, scroll), MinDistance, MaxDistance);

    /// <summary>Pan the target along the camera-relative ground plane (x = strafe, y = forward).</summary>
    public void Pan(Vector2 move, float dt)
    {
        var forward = Vector3.Normalize(new Vector3(-MathF.Sin(Yaw), 0, -MathF.Cos(Yaw)));
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        Target += (right * move.X + forward * move.Y) * (Distance * 0.6f * dt);
    }

    public Matrix4x4 View() => Matrix4x4.CreateLookAt(Eye, Target, Vector3.UnitY);
}
