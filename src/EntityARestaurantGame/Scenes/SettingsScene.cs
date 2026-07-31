using System.Numerics;
using Engine.Render;
using Engine.Ui;

namespace EntityARestaurantGame.Scenes;

/// <summary>
/// Settings skeleton — proves push/pop depth on the scene stack. The real
/// pages (video / audio / controls / gameplay) land per the build order in
/// specs/UI_2D_Interface_Plan.md.
/// </summary>
public sealed class SettingsScene : Scene
{
    private const float PanelW = 260f;
    private const float PanelH = 170f;

    private MenuList _menu = null!;

    public override bool IsOverlay => true;

    protected override void OnLoad()
    {
        _menu = new MenuList(
        [
            new MenuItem { Label = "Video — soon",    IsEnabled = false },
            new MenuItem { Label = "Audio — soon",    IsEnabled = false },
            new MenuItem { Label = "Controls — soon", IsEnabled = false },
            new MenuItem { Label = "Back",            Execute = () => SceneAction.Pop },
        ]);
    }

    public override SceneAction Update(double dt, in UiInput input)
    {
        if (input.Cancel) return SceneAction.Pop;
        return _menu.Handle(input);
    }

    public override void Draw(Renderer2D r)
    {
        float x = (Ctx.Width - PanelW) / 2f;
        float y = (Ctx.Height - PanelH) / 2f;
        r.DrawRect(x, y, PanelW, PanelH, new Vector4(0.10f, 0.10f, 0.12f, 0.97f));
        r.DrawRectOutline(x, y, PanelW, PanelH, new Vector4(0.40f, 0.42f, 0.52f, 1f));

        float centerX = Ctx.Width / 2f;
        r.DrawTextCentered(Ctx.HeadingFont, "SETTINGS", centerX, y + 14, new Vector4(0.80f, 0.83f, 0.92f, 1f));
        _menu.Draw(r, Ctx.Font, centerX, y + 64);
    }
}
