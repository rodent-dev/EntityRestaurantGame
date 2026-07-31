using System.Numerics;
using Game.Contracts;

namespace EntityARestaurantGame.Render3D;

/// <summary>
/// Maps an entity's standing toward the favored pawn onto an outline color:
/// gold for the pawn itself, blue → grey → red across their stored relation,
/// and warning yellow for aggressive-tempered strangers (the bear tier).
/// Pure presentation — reads sim truth, never writes it.
/// </summary>
public static class RelationTint
{
    /// <summary>The favored pawn's own color — also the player HUD accent.</summary>
    public static readonly Vector4 Self = new(1f, 0.83f, 0.45f, 1f);

    private static readonly Vector4 Warm    = new(0.30f, 0.55f, 1f, 1f);
    private static readonly Vector4 Neutral = new(0.58f, 0.58f, 0.58f, 1f);
    private static readonly Vector4 Hostile = new(0.95f, 0.25f, 0.20f, 1f);
    private static readonly Vector4 Menace  = new(0.95f, 0.82f, 0.20f, 1f);

    /// <summary>Relation magnitude that reads as fully warm/hostile.</summary>
    private const float Saturation = 5f;

    /// <summary>Relations this shallow still count as "stranger" for the menace tier.</summary>
    private const int StrangerBand = 1;

    public static Vector4 For(Entity entity, string favoredId)
    {
        if (entity.ID == favoredId) return Self;

        int rel = entity.EntityRelationships.GetValueOrDefault(favoredId);

        // Keywords, not names: "potentially evil" is a temperament — an
        // appetite for Aggressive action — not a species check. Once a real
        // relation forms either way, it takes over: a bear that hates you
        // reads red, a tamed one reads blue.
        if (Math.Abs(rel) <= StrangerBand && IsMenacing(entity))
            return Menace;

        float t = Math.Clamp(rel / Saturation, -1f, 1f);
        return t >= 0
            ? Vector4.Lerp(Neutral, Warm, t)
            : Vector4.Lerp(Neutral, Hostile, -t);
    }

    private static bool IsMenacing(Entity entity) =>
        entity.Behaviors.Any(b => b.Affect > 0 && b.Keywords.Contains("Aggressive"));
}
