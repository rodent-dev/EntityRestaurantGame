using System.Numerics;
using Engine.Render;
using Engine.Ui;
using Game.Contracts;

namespace EntityARestaurantGame.Scenes;

/// <summary>
/// The whisper menu: nudge the active pawn's next action, and — since a verb
/// without an object is half a thought — say who to aim it at. Pick an action,
/// then pick a target from whoever that action can actually be used on.
/// Self-targeted verbs skip the second step.
///
/// Because walking is an action now, "Approach → Sera" is an ordinary whisper
/// rather than a special movement command, and the reach gate means any
/// targeted whisper implies the walk to get there.
///
/// Left-docked and non-blocking on purpose: the sim keeps deciding while you
/// choose, which is the point — a whisper is a bid, not an order.
/// </summary>
public sealed class InfluenceScene : Scene
{
    private const float PanelW = 150f;
    private const int MaxRows = 9;

    private readonly EntityController _controller;
    private MenuList? _menu;
    private IAction? _pendingAction;

    public override bool BlocksSim => false;
    public override bool IsOverlay => true;

    public InfluenceScene(EntityController controller) => _controller = controller;

    protected override void OnLoad() => BuildActionMenu();

    /// <summary>Stage one: every verb the pawn owns.</summary>
    private void BuildActionMenu()
    {
        if (_controller.FavoredEntity is not { } pawn) return;
        _pendingAction = null;

        var items = pawn.Actions.GetAllActions()
            .Where(a => a.CanActorUse(pawn))
            .OrderBy(a => a.ActionName)
            .Select(a => new MenuItem
            {
                Label = a.ActionName,
                Execute = () => Choose(pawn, a),
            })
            .ToList();

        items.Add(new MenuItem
        {
            Label = "(clear whisper)",
            Execute = () =>
            {
                pawn.PendingInfluence = null;
                return SceneAction.Pop;
            }
        });

        _menu = new MenuList(items) { VisibleRows = MaxRows };
    }

    /// <summary>
    /// A verb aimed only at its own user needs no object — whisper it and be
    /// done. Anything else opens the target list, so the player can say WHO.
    /// </summary>
    private SceneAction Choose(Entity pawn, IAction action)
    {
        var targets = _controller.ActiveEntities
            .Where(e => e.ID != pawn.ID && action.CanTarget(pawn, e))
            .ToList();

        if (targets.Count == 0)
        {
            Whisper(pawn, action, target: null);
            return SceneAction.Pop;
        }

        _pendingAction = action;
        var items = targets
            .OrderBy(e => Vector3.Distance(pawn.Position, e.Position))
            .Select(e => new MenuItem
            {
                Label = _controller.IsOurs(e) ? $"{e.DisplayName} *" : e.DisplayName,
                Execute = () =>
                {
                    Whisper(pawn, action, e);
                    return SceneAction.Pop;
                }
            })
            .ToList();

        // Self-usable verbs keep the option of no object at all.
        if (action.CanTarget(pawn, pawn))
            items.Insert(0, new MenuItem
            {
                Label = "(no one in particular)",
                Execute = () =>
                {
                    Whisper(pawn, action, target: null);
                    return SceneAction.Pop;
                }
            });

        _menu = new MenuList(items) { VisibleRows = MaxRows };
        return SceneAction.Stay;
    }

    private static void Whisper(Entity pawn, IAction action, Entity? target) =>
        pawn.PendingInfluence = new Influence
        {
            TargetActionName = action.ActionName,
            TargetEntityId = target?.ID,
            Strength = EntityController.InfluenceStrength,
            Source = "Player",
        };

    public override SceneAction Update(double dt, in UiInput input)
    {
        if (_menu is null || _controller.FavoredEntity is not { IsAlive: true })
            return SceneAction.Pop;

        // Cancel backs out of the target list before it closes the menu, so a
        // misfired pick costs one keystroke rather than the whole interaction.
        if (input.Cancel)
        {
            if (_pendingAction is null) return SceneAction.Pop;
            BuildActionMenu();
            return SceneAction.Stay;
        }

        return _menu.Handle(input);
    }

    public override void Draw(Renderer2D r)
    {
        if (_menu is null || _controller.FavoredEntity is not { } pawn) return;

        var font = Ctx.Font;
        float line = font.LineHeight + 3;
        float pad = 6f;

        var title = new Vector4(0.92f, 0.85f, 0.70f, 1f);
        var dim = new Vector4(0.55f, 0.52f, 0.46f, 1f);
        var body = new Vector4(0.85f, 0.83f, 0.76f, 1f);

        int rows = Math.Min(_menu.Count, MaxRows) + 2; // + ellipsis allowance
        float headerH = line * 3 + 6;
        float panelH = pad * 2 + headerH + rows * line + line;

        r.DrawRect(0, 0, PanelW, panelH, new Vector4(0.07f, 0.08f, 0.10f, 0.92f));
        r.DrawRectOutline(0, 0, PanelW, panelH, new Vector4(0.55f, 0.45f, 0.28f, 1f));

        float y = pad;
        r.DrawText(font, _pendingAction is null
            ? $"Whisper: {pawn.DisplayName}"
            : $"{_pendingAction.ActionName} at whom?", pad, y, title);
        y += line;

        // A gloomy pawn half-listens: influence lands scaled by mood.
        r.DrawText(font,
            $"mood heeds {(int)(pawn.Needs.MoodRatio * 100)}%  (+{(int)(EntityController.InfluenceStrength * pawn.Needs.MoodRatio)})",
            pad, y, dim);
        y += line;

        r.DrawText(font, Standing(pawn), pad, y, pawn.PendingInfluence is null ? dim : body);
        y += line + 6;

        _menu.Draw(r, font, PanelW / 2f, y);

        r.DrawText(font, _pendingAction is null ? "Esc closes" : "Esc goes back",
            pad, panelH - line - 2, dim);
    }

    /// <summary>The standing whisper, named with its object when it has one.</summary>
    private string Standing(Entity pawn)
    {
        if (pawn.PendingInfluence is not { } inf) return "standing: none";
        if (inf.TargetEntityId is null) return $"standing: {inf.TargetActionName}";

        string who = _controller.ActiveEntities
            .FirstOrDefault(e => e.ID == inf.TargetEntityId)?.DisplayName ?? "?";
        return $"standing: {inf.TargetActionName} -> {who}";
    }
}
