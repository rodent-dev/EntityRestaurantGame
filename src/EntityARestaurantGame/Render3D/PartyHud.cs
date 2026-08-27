using System.Numerics;
using Engine.Render;
using Game.Contracts;

namespace EntityARestaurantGame.Render3D;

/// <summary>
/// The party strip: one tab per member across the bottom, the active one
/// expanded into the dossier that used to be PlayerHud. Clicking a tab (or
/// pressing its number) makes that member the one the narrator speaks
/// through — which is also who the camera follows and who relation colours
/// are measured against.
///
/// Host-owned and passive like DebugHud: it reports a click, it never pauses
/// the sim. Hidden until the intro picks a pawn.
/// </summary>
public sealed class PartyHud
{
    private const float PanelW = 168f;
    private const float TabH = 10f;

    private static readonly Vector4 Backdrop = new(0.07f, 0.08f, 0.10f, 0.85f);
    private static readonly Vector4 TabIdle = new(0.13f, 0.14f, 0.16f, 0.95f);
    private static readonly Vector4 Body = new(0.85f, 0.83f, 0.76f, 1f);
    private static readonly Vector4 Dim = new(0.55f, 0.52f, 0.46f, 1f);
    private static readonly Vector4 Bad = new(1f, 0.45f, 0.35f, 1f);
    private static readonly Vector4 HpFill = new(0.45f, 0.70f, 0.35f, 1f);
    private static readonly Vector4 BarBack = new(0.20f, 0.22f, 0.24f, 1f);

    /// <summary>
    /// How much of the bottom edge this strip occupies, tabs included.
    /// </summary>
    /// <remarks>
    /// Published because scenes docked to the left edge have to know where to
    /// stop, and the only alternative is a magic number in the scene that
    /// silently goes wrong the day the font changes. The strip owns this
    /// measurement; everyone else asks.
    /// </remarks>
    public static float Footprint(SpriteFont font, SpriteFont small) =>
        Pad * 2 + (font.LineHeight + 1) + 6 + (small.LineHeight + 1) * 4 + TabH;

    private const float Pad = 4f;

    /// <summary>Where each member's tab landed last frame, for hit-testing.</summary>
    private readonly List<(string Id, float X, float Y, float W)> _tabRects = new();

    /// <summary>
    /// Which member a canvas-space click landed on, or null. The host turns
    /// that into a favored-pawn switch.
    /// </summary>
    public string? MemberAt(Vector2 canvasPoint)
    {
        foreach (var (id, x, y, w) in _tabRects)
            if (canvasPoint.X >= x && canvasPoint.X < x + w &&
                canvasPoint.Y >= y && canvasPoint.Y < y + TabH)
                return id;
        return null;
    }

    /// <summary>Draw inside an active Renderer2D pass.</summary>
    public void Draw(Renderer2D r, SpriteFont font, SpriteFont small,
        EntityController controller, int canvasHeight)
    {
        _tabRects.Clear();
        if (controller.FavoredEntity is not { } active) return;

        var members = controller.PlayerPartyMembers;
        if (members.Count == 0) members = [active];

        float pad = Pad;
        float line = small.LineHeight + 1;
        float nameLine = font.LineHeight + 1;
        float panelH = Footprint(font, small) - TabH;
        float tabRow = canvasHeight - panelH - TabH;
        float y = canvasHeight - panelH;

        DrawTabs(r, small, members, active, tabRow);

        r.DrawRect(0, y, PanelW, panelH, Backdrop);
        r.DrawRectOutline(0, y, PanelW, panelH, RelationTint.Self);

        float tx = pad + 2;
        y += pad;
        r.DrawText(font, $"{active.DisplayName}  ({active.EntityType})", tx, y, RelationTint.Self);
        y += nameLine;

        float barW = PanelW - tx * 2;
        float ratio = Math.Clamp((float)(active.GetCurrentHealth() / Math.Max(1.0, active.GetMaxHealth())), 0f, 1f);
        r.DrawRect(tx, y, barW, 4, BarBack);
        r.DrawRect(tx, y, barW * ratio, 4, ratio < 0.35f ? Bad : HpFill);
        y += 6;

        r.DrawText(small,
            $"hp {active.GetCurrentHealth():0}/{active.GetMaxHealth():0}  hun {active.Needs.Hunger}  mood {active.Needs.Mood}",
            tx, y, Body);
        y += line;

        var last = controller.GetDecisionHistory(active.ID).LastOrDefault();
        r.DrawText(small, last is null ? "doing: -" : $"doing: {last.ActionName} -> {last.TargetName}",
            tx, y, Body);
        y += line;

        r.DrawText(small,
            active.PendingInfluence is { } inf
                ? $"whisper: {inf.Label}{(inf.TargetEntityId is null ? "" : " ->")} (+{inf.Strength})"
                : "whisper: none",
            tx, y, active.PendingInfluence is null ? Dim : Body);
        y += line;

        r.DrawText(small, "[Tab] whisper  [F] follow  [Q] swap", tx, y, Dim);
    }

    /// <summary>One tab per member, active one lit. Records rectangles so the
    /// host can hit-test clicks against what was actually drawn.</summary>
    private void DrawTabs(Renderer2D r, SpriteFont small, IReadOnlyList<Entity> members,
        Entity active, float y)
    {
        float x = 0;
        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            // No index prefix: nothing is bound to the number keys, and a "1."
            // on the tab would promise a shortcut that does not exist.
            string label = member.DisplayName;
            float w = small.Measure(label).X + 6;

            bool isActive = member.ID == active.ID;
            r.DrawRect(x, y, w, TabH, isActive ? Backdrop : TabIdle);
            if (isActive) r.DrawRect(x, y, w, 1, RelationTint.Self);

            // A hurt member reads red on the tab, so you notice without
            // having to swap to them.
            float ratio = (float)(member.GetCurrentHealth() / Math.Max(1.0, member.GetMaxHealth()));
            r.DrawText(small, label, x + 3, y + 1,
                ratio < 0.35f ? Bad : isActive ? RelationTint.Self : Dim);

            _tabRects.Add((member.ID, x, y, w));
            x += w + 1;
        }
    }
}
