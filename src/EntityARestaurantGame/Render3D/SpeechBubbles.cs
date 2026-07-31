using System.Numerics;
using Engine.Render;

namespace EntityARestaurantGame.Render3D;

/// <summary>
/// The stand-in for action animations: each pawn broadcasts its latest LOUD
/// personal-log entry (the distinct, value-changing tier — speech, deals,
/// violence) in a small text box above its head while the entry is fresh.
/// Quiet entries stay in the inspect panel. This is explicitly the channel
/// conversations render through going forward (item PNGs alongside the text
/// are a future project).
/// </summary>
public sealed class SpeechBubbles
{
    private const int FreshTicks = 8;       // sim ticks a line stays overhead
    private const int MaxChars = 36;
    private const float HeadHeight = 2.15f; // render-units above the pawn's feet

    public void Draw(Renderer2D r, SpriteFont font, EntityController controller,
        IReadOnlyDictionary<string, Vector3> visualPos, in Matrix4x4 viewProj,
        int canvasW, int canvasH)
    {
        foreach (var e in controller.ActiveEntities)
        {
            var entry = controller.LatestBroadcast(e.ID);
            if (entry is null || controller.CurrentTick - entry.Tick > FreshTicks) continue;
            if (!visualPos.TryGetValue(e.ID, out var pos)) continue;

            // World → canvas: same row-vector convention the pawn draw uses.
            var clip = Vector4.Transform(new Vector4(pos + new Vector3(0, HeadHeight, 0), 1f), viewProj);
            if (clip.W <= 0.05f) continue; // behind the camera
            float sx = (clip.X / clip.W * 0.5f + 0.5f) * canvasW;
            float sy = (1f - (clip.Y / clip.W * 0.5f + 0.5f)) * canvasH;
            if (sx < -80 || sx > canvasW + 80 || sy < -20 || sy > canvasH + 20) continue;

            string line = Condense(e.DisplayName, entry.Message);
            if (line.Length == 0) continue;

            float w = font.Measure(line).X;
            float h = font.LineHeight + 2;
            float bx = sx - w / 2f - 3;
            float by = sy - h;
            r.DrawRect(bx, by, w + 6, h, new Vector4(0.05f, 0.05f, 0.07f, 0.72f));
            r.DrawText(font, line, bx + 3, by + 1, new Vector4(0.92f, 0.90f, 0.82f, 1f));
        }
    }

    /// <summary>Bubble-ify a log line: the speaker is implied by position, so
    /// their name prefix drops; then clamp to bubble width.</summary>
    private static string Condense(string name, string message)
    {
        string line = message;
        if (line.StartsWith(name, StringComparison.Ordinal))
        {
            line = line[name.Length..].TrimStart();
            if (line.StartsWith(':')) line = line[1..].TrimStart();
        }
        if (line.Length > MaxChars) line = line[..(MaxChars - 3)] + "...";
        return line;
    }
}
