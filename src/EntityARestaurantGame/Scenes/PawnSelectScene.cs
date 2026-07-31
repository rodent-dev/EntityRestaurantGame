using System.Numerics;
using Engine.Render;
using Engine.Ui;
using Game.Contracts;

namespace EntityARestaurantGame.Scenes;

/// <summary>
/// The windowed intro (heir of the console PawnSelectScreen): the player is a
/// storyteller above the board and picks the pawn to act through. Blocks the
/// sim — the world holds its breath until someone is chosen — and the host
/// re-pushes it when the favored pawn dies. There is no cancel path: the
/// narrator must settle behind someone's eyes.
/// </summary>
public sealed class PawnSelectScene : Scene
{
    /// <summary>Rows on screen at once; the list scrolls past this rather than
    /// truncating, so a full settlement is all reachable.</summary>
    private const int VisibleChoices = 8;
    private const float PanelW = 320f;

    private readonly EntityController _controller;
    private List<Entity> _choices = [];
    private MenuList? _menu;

    public override bool IsOverlay => true; // the board stays visible behind the choice

    public PawnSelectScene(EntityController controller) => _controller = controller;

    protected override void OnLoad()
    {
        _choices = [.. _controller.ActiveEntities];
        if (_choices.Count == 0) return;

        _menu = new MenuList(_choices.Select(e => new MenuItem
        {
            Label = $"{e.DisplayName} the {e.EntityType}",
            Execute = () =>
            {
                _controller.FavoredEntityId = e.ID;
                _controller.Announce($"The narrator settles behind {e.DisplayName}'s eyes", e, loud: true);
                return SceneAction.Pop;
            }
        })) { VisibleRows = VisibleChoices };
    }

    public override SceneAction Update(double dt, in UiInput input)
    {
        if (_menu is null) // nobody left to play — the run is over
            return input.Confirm || input.Cancel ? SceneAction.Quit : SceneAction.Stay;
        return _menu.Handle(input);
    }

    public override void Draw(Renderer2D r)
    {
        r.DrawRect(0, 0, Ctx.Width, Ctx.Height, new Vector4(0, 0, 0, 0.55f));

        var title = new Vector4(0.92f, 0.85f, 0.70f, 1f);
        var dim = new Vector4(0.55f, 0.52f, 0.46f, 1f);

        float line = Ctx.Font.LineHeight + 3;
        float menuTop = 58f;
        float panelH = _menu is null
            ? menuTop + line * 2
            : menuTop + _choices.Count * line + line * 2 + 6;
        float x = (Ctx.Width - PanelW) / 2f;
        float y = (Ctx.Height - panelH) / 2f;

        r.DrawRect(x, y, PanelW, panelH, new Vector4(0.10f, 0.09f, 0.08f, 0.95f));
        r.DrawRectOutline(x, y, PanelW, panelH, new Vector4(0.55f, 0.45f, 0.28f, 1f));

        float centerX = Ctx.Width / 2f;
        r.DrawTextCentered(Ctx.HeadingFont, "CHOOSE YOUR PAWN", centerX, y + 8, title);
        r.DrawTextCentered(Ctx.Font, "Your pawn has its own mind - you nudge, it decides.",
            centerX, y + 40, dim);

        if (_menu is null)
        {
            r.DrawTextCentered(Ctx.Font, "The board is empty. (Enter to quit)", centerX, y + menuTop, dim);
            return;
        }

        _menu.Draw(r, Ctx.Font, centerX, y + menuTop);

        // Dossier lines for the highlighted pawn: temperament + pockets.
        var pick = _choices[_menu.Selected];
        string traits = pick.Behaviors.Count == 0
            ? "(no temperament)"
            : string.Join(", ", pick.Behaviors.Select(b => b.Name));
        string carries = pick.Inventory.Count == 0
            ? "empty-handed"
            : string.Join(", ", pick.Inventory.GroupBy(i => i.ItemName)
                .Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key));

        float dy = y + menuTop + _choices.Count * line + 4;
        r.DrawTextCentered(Ctx.Font, traits, centerX, dy, dim);
        r.DrawTextCentered(Ctx.Font, carries, centerX, dy + line, dim);
    }
}
