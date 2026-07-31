namespace Game.Contracts;

/// <summary>
/// The shared axis vocabulary claims and preferences are keyed on. Axes are
/// plain strings (same philosophy as action keywords) — this class just names
/// the canonical set so data files, preference sheets, and descriptors agree.
/// </summary>
public static class Axes
{
    public const string Nourishment = "Nourishment";
    public const string Comfort     = "Comfort";
    public const string Safety      = "Safety";
    public const string Harm        = "Harm";
    public const string Glory       = "Glory";
    public const string Commerce    = "Commerce";
    public const string Beauty      = "Beauty";
    public const string Craft       = "Craft";
    public const string Obligation  = "Obligation";

    public static readonly string[] All =
        [Nourishment, Comfort, Safety, Harm, Glory, Commerce, Beauty, Craft, Obligation];
}
