using System.Numerics;
using Engine.Render;
using Engine.Ui;

namespace EntityARestaurantGame.Scenes;

/// <summary>
/// The pause menu — the scene controller's proof piece. Dims the live 3D view,
/// draws a panel (data/ui/pause_bg.png if present, plain rect otherwise), and
/// offers Resume / Settings / Quit.
/// </summary>
public sealed class PauseScene : Scene
{
    private const float PanelW = 220f;
    private const float PanelH = 150f;

    private Texture2D? _background;
    private MenuList _menu = null!;

    public override bool IsOverlay => true;

    protected override void OnLoad()
    {
        _background = Ctx.TryLoadTexture("pause_bg.png");
        _menu = new MenuList(
        [
            new MenuItem { Label = "Resume",   Execute = () => SceneAction.Pop },
            new MenuItem { Label = "Settings", Execute = () => SceneAction.Push(new SettingsScene()) },
            new MenuItem { Label = "Quit",     Execute = () => SceneAction.Quit },
        ]);
    }

    public override SceneAction Update(double dt, in UiInput input)
    {
        if (input.Cancel) return SceneAction.Pop;
        return _menu.Handle(input);
    }

    public override void Draw(Renderer2D r)
    {
        r.DrawRect(0, 0, Ctx.Width, Ctx.Height, new Vector4(0, 0, 0, 0.55f));

        float x = (Ctx.Width - PanelW) / 2f;
        float y = (Ctx.Height - PanelH) / 2f;
        if (_background is not null)
            r.DrawTexture(_background, x, y, PanelW, PanelH);
        else
            r.DrawRect(x, y, PanelW, PanelH, new Vector4(0.13f, 0.11f, 0.09f, 0.95f));
        r.DrawRectOutline(x, y, PanelW, PanelH, new Vector4(0.55f, 0.45f, 0.28f, 1f));

        float centerX = Ctx.Width / 2f;
        r.DrawTextCentered(Ctx.HeadingFont, "PAUSED", centerX, y + 14, new Vector4(0.92f, 0.85f, 0.70f, 1f));
        _menu.Draw(r, Ctx.Font, centerX, y + 62);

        r.DrawTextCentered(Ctx.Font, "Up/Down  Enter  Esc", centerX, y + PanelH - Ctx.Font.LineHeight - 6,
            new Vector4(0.5f, 0.47f, 0.42f, 1f));
    }
}
