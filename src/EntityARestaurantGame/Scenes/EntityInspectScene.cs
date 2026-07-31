using System.Numerics;
using Engine.Render;
using Engine.Ui;
using Game.Contracts;

namespace EntityARestaurantGame.Scenes;

/// <summary>
/// The dossier on somebody — a detached window you can drag where you like,
/// with two tabs: what they ARE (vitals, standing, what they carry) and what
/// they have DONE. The sim keeps running underneath; this is furniture, not a
/// mode. Clicking another pawn retargets the open window rather than stacking
/// a second one (the host calls Retarget).
/// </summary>
public sealed class EntityInspectScene : Scene
{
    private const int TabStatus = 0;
    private const int TabLog = 1;

    private readonly EntityController _controller;
    private readonly DragWindow _window;
    private Entity _entity;
    private int _scroll; // log entries skipped from the newest end

    public override bool BlocksSim => false;
    public override bool IsOverlay => true;

    public EntityInspectScene(EntityController controller, Entity entity)
    {
        _controller = controller;
        _entity = entity;
        _window = new DragWindow(entity.DisplayName, new Vector2(300, 30), new Vector2(172, 190),
            "Status", "Log");
    }

    /// <summary>Point the open window at another pawn without re-pushing the
    /// scene — the window stays exactly where the player parked it.</summary>
    public void Retarget(Entity entity)
    {
        _entity = entity;
        _window.Title = entity.DisplayName;
        _scroll = 0;
    }

    public override SceneAction Update(double dt, in UiInput input)
    {
        _window.Handle(input, Ctx.Font, Ctx.Width, Ctx.Height);
        if (_window.CloseRequested || input.Cancel) return SceneAction.Pop;

        if (input.Confirm) _window.NextTab();
        if (input.Up) _scroll++;
        if (input.Down) _scroll = Math.Max(0, _scroll - 1);
        return SceneAction.Stay;
    }

    public override void Draw(Renderer2D r)
    {
        var font = Ctx.Font;
        _window.Draw(r, font);

        var (bx, by, bw, _) = _window.Body(font);
        if (_window.ActiveTab == TabLog) DrawLog(r, font, bx, by, bw);
        else DrawStatus(r, font, bx, by, bw);
    }

    private static readonly Vector4 Title = new(0.92f, 0.85f, 0.70f, 1f);
    private static readonly Vector4 Dim = new(0.55f, 0.52f, 0.46f, 1f);
    private static readonly Vector4 Body = new(0.85f, 0.83f, 0.76f, 1f);
    private static readonly Vector4 Faded = new(0.62f, 0.60f, 0.55f, 1f);

    /// <summary>Who they are: vitals, where they stand with us, what they carry.</summary>
    private void DrawStatus(Renderer2D r, SpriteFont font, float x, float y, float width)
    {
        float line = font.LineHeight + 1;
        float bottom = _window.Position.Y + _window.Size.Y - line - 2;

        r.DrawText(font, _entity.IsAlive ? _entity.EntityType : $"{_entity.EntityType}  (dead)", x, y, Dim);
        y += line;
        r.DrawText(font, $"HP {_entity.GetCurrentHealth():0}/{_entity.GetMaxHealth():0}", x, y, Body);
        y += line;
        r.DrawText(font, $"Hunger {_entity.Needs.Hunger}  Mood {_entity.Needs.Mood}", x, y, Body);
        y += line;

        // Standing: which band, and how they feel about us. The two facts that
        // decide whether asking them along is worth a beat.
        string band = _controller.Parties?.PartyOf(_entity)?.Name ?? "unaffiliated";
        r.DrawText(font, band, x, y, _controller.IsOurs(_entity) ? Title : Dim);
        y += line;

        if (_controller.FavoredEntity is { } pawn && pawn.ID != _entity.ID)
        {
            int regard = _entity.EntityRelationships.GetValueOrDefault(pawn.ID);
            r.DrawText(font, $"regards you {regard:+0;-0;0}", x, y, regard < 0 ? Faded : Body);
            y += line;
        }
        y += 3;

        if (_entity.Inventory.Count == 0)
        {
            r.DrawText(font, "(empty-handed)", x, y, Dim);
            return;
        }

        foreach (var group in _entity.Inventory.GroupBy(i => i.ItemName).OrderBy(g => g.Key))
        {
            if (y > bottom) return;
            string label = group.Count() > 1 ? $"{group.Key} x{group.Count()}" : group.Key;
            if (group.Any(i => i.UsesRemaining >= 0))
                label += $"  ({group.Where(i => i.UsesRemaining >= 0).Sum(i => i.UsesRemaining)} uses)";
            r.DrawText(font, label, x, y, Body);
            y += line;
        }
    }

    /// <summary>The personal log, newest first. Their own deeds read brighter
    /// than things merely done to or around them.</summary>
    private void DrawLog(Renderer2D r, SpriteFont font, float x, float y, float width)
    {
        float line = font.LineHeight + 1;
        float bottom = _window.Position.Y + _window.Size.Y - line - 2;

        var log = _controller.GetLog(_entity.ID);
        if (log.Count == 0)
        {
            r.DrawText(font, "(nothing yet)", x, y, Dim);
            return;
        }

        _scroll = Math.Min(_scroll, log.Count - 1);
        for (int i = log.Count - 1 - _scroll; i >= 0; i--)
        {
            var entry = log[i];
            foreach (string wrapped in Wrap(font, $"t{entry.Tick} {entry.Message}", width))
            {
                if (y > bottom) return;
                r.DrawText(font, wrapped, x, y, entry.AsActor ? Body : Faded);
                y += line;
            }
            y += 2;
        }
    }

    /// <summary>Greedy word wrap against the real font metrics.</summary>
    private static IEnumerable<string> Wrap(SpriteFont font, string text, float maxWidth)
    {
        string current = "";
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = current.Length == 0 ? word : $"{current} {word}";
            if (current.Length > 0 && font.Measure(candidate).X > maxWidth)
            {
                yield return current;
                current = word;
            }
            else
            {
                current = candidate;
            }
        }
        if (current.Length > 0) yield return current;
    }
}
