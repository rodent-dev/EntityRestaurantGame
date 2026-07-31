using System.Numerics;
using Engine.Render;

namespace EntityARestaurantGame.Render3D;

/// <summary>
/// Text debug view over the 3D world — the windowed heir of the console
/// WorldScreen. Host-owned rather than a Scene: it never takes input, never
/// pauses the sim, and draws above everything. Toggle with F3.
/// </summary>
public sealed class DebugHud
{
    public bool Visible { get; set; }

    private static readonly Vector4 Backdrop = new(0, 0, 0, 0.6f);
    private static readonly Vector4 Header = new(1f, 0.85f, 0.45f, 1f);
    private static readonly Vector4 Body = new(0.85f, 0.88f, 0.85f, 1f);
    private static readonly Vector4 Dim = new(0.55f, 0.58f, 0.55f, 1f);
    private static readonly Vector4 Bad = new(1f, 0.45f, 0.35f, 1f);

    private const int SignalLines = 7;

    /// <summary>Draw inside an active Renderer2D pass.</summary>
    public void Draw(Renderer2D r, SpriteFont font, EntityController controller,
        double fps, bool simPaused, int canvasWidth, int canvasHeight)
    {
        if (!Visible) return;

        float pad = 3f;
        float line = font.LineHeight;

        // ── Top-left: clock + roster ────────────────────────────────────
        var entities = controller.ActiveEntities;
        float panelH = pad * 2 + line * (1 + entities.Count * 2);
        r.DrawRect(0, 0, 232, panelH, Backdrop);

        string paused = simPaused ? "  PAUSED" : "";
        r.DrawText(font, $"tick {controller.CurrentTick}  fps {fps:0}  alive {entities.Count}{paused}",
            pad, pad, Header);

        float y = pad + line;
        foreach (var e in entities)
        {
            string hp = $"{e.GetCurrentHealth():0}/{e.GetMaxHealth():0}";
            r.DrawText(font,
                $"{e.DisplayName} [{e.EntityType}]  hp {hp}  hun {e.Needs.Hunger}  mood {e.Needs.Mood}",
                pad, y, e.GetCurrentHealth() < e.GetMaxHealth() * 0.35 ? Bad : Body);
            y += line;

            var last = controller.GetDecisionHistory(e.ID).LastOrDefault();
            string doing = last is null ? "-" : $"{last.ActionName} -> {last.TargetName} ({last.PrimaryFactor})";
            r.DrawText(font,
                $"  @({e.Position.X:0.0},{e.Position.Y:0.0}) {controller.LocationTag(e)}  {doing}",
                pad, y, Dim);
            y += line;
        }

        // ── Bottom: signal log tail (the console announcements) ─────────
        var signals = controller.Signals;
        int count = Math.Min(SignalLines, signals.Count);
        if (count > 0)
        {
            float logH = pad * 2 + line * count;
            float logY = canvasHeight - logH;
            r.DrawRect(0, logY, canvasWidth, logH, Backdrop);
            for (int i = 0; i < count; i++)
            {
                var s = signals[signals.Count - count + i];
                r.DrawText(font, $"t{s.Tick,4}  {s.Message}", pad, logY + pad + i * line, Body);
            }
        }
    }
}
