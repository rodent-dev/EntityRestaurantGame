using Game.Contracts;

namespace EntityARestaurantGame.UI.Screens;

/// <summary>
/// The narrator's second lever, next to Influence: inject a claim into one
/// entity's digestion pipeline and watch the stance gates do their work.
/// Spoken through your favored pawn when you have one (their credibility on
/// the line), otherwise it lands as an anonymous whisper at neutral trust.
/// Three picks: subject → axis → slant.
/// </summary>
public class RumorScreen : IScreen
{
    private enum Stage { Subject, Axis, Slant }

    private readonly Entity _receiver;
    private readonly EntityController _controller;

    private Stage _stage = Stage.Subject;
    private (string Id, string Name)? _subject;
    private string? _axis;

    public RumorScreen(Entity receiver, EntityController controller)
    {
        _receiver = receiver;
        _controller = controller;
    }

    public string Title => $"Whisper to {_receiver.DisplayName}";

    private Entity? Mouthpiece =>
        _controller.FavoredEntity is { } pawn && pawn.ID != _receiver.ID ? pawn : null;

    public void Render(ConsoleRenderer renderer)
    {
        renderer.DrawStatusBar($"Tick: {_controller.CurrentTick}  |  {Title}");

        var lines = new List<string>
        {
            Mouthpiece is { } pawn
                ? $"Spoken through {pawn.DisplayName} — their credibility carries it."
                : "No pawn to speak through: an anonymous whisper (neutral trust).",
            string.Empty
        };

        switch (_stage)
        {
            case Stage.Subject:
                lines.Add("What is the rumor about?");
                lines.Add(string.Empty);
                var subjects = Subjects();
                for (int i = 0; i < subjects.Count; i++)
                    lines.Add($"  [{KeyFor(i)}] {subjects[i].Name}");
                break;

            case Stage.Axis:
                lines.Add($"Subject: {_subject!.Value.Name}");
                lines.Add("Which axis does it speak to?");
                lines.Add(string.Empty);
                for (int i = 0; i < Axes.All.Length; i++)
                    lines.Add($"  [{KeyFor(i)}] {Axes.All[i]}");
                break;

            case Stage.Slant:
                lines.Add($"Subject: {_subject!.Value.Name}   Axis: {_axis}");
                lines.Add("And the slant?");
                lines.Add(string.Empty);
                lines.Add("  [1] Praise        (+0.5)");
                lines.Add("  [2] Smear         (-0.5)");
                lines.Add("  [3] Glowing tale  (+0.8)");
                lines.Add("  [4] Vicious lie   (-0.8)");
                break;
        }

        renderer.DrawPanel(Title, lines);
        renderer.WriteLine();
        renderer.WriteLine("  [Esc] Back");
    }

    public ScreenAction HandleInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape) return ScreenAction.Pop;

        int index = IndexFor(key.KeyChar);
        if (index < 0) return ScreenAction.Stay;

        switch (_stage)
        {
            case Stage.Subject:
                var subjects = Subjects();
                if (index >= subjects.Count) return ScreenAction.Stay;
                _subject = subjects[index];
                _stage = Stage.Axis;
                return ScreenAction.Stay;

            case Stage.Axis:
                if (index >= Axes.All.Length) return ScreenAction.Stay;
                _axis = Axes.All[index];
                _stage = Stage.Slant;
                return ScreenAction.Stay;

            case Stage.Slant:
                double? valence = index switch { 0 => 0.5, 1 => -0.5, 2 => 0.8, 3 => -0.8, _ => null };
                if (valence == null) return ScreenAction.Stay;

                var claim = new Claim(_subject!.Value.Id, _subject.Value.Name, _axis!, valence.Value);
                _controller.Dialog!.Whisper(_receiver, claim, Mouthpiece);
                _controller.FlushDialog();
                return ScreenAction.Replace(new MindScreen(_receiver, _controller));
        }

        return ScreenAction.Stay;
    }

    /// <summary>Anything descriptor-bearing in the world: people, then items.</summary>
    private List<(string Id, string Name)> Subjects()
    {
        var subjects = _controller.ActiveEntities
            .Select(e => (e.ID, e.DisplayName))
            .ToList();

        subjects.AddRange(_controller.ActiveEntities
            .SelectMany(e => e.Inventory)
            .Where(i => i.Descriptors.Count > 0)
            .DistinctBy(i => i.ItemId)
            .Select(i => (i.ItemId, i.ItemName)));

        return subjects;
    }

    // 1-9 then a-z, so item lists don't outrun the digit keys.
    private static char KeyFor(int index) => index < 9 ? (char)('1' + index) : (char)('a' + index - 9);

    private static int IndexFor(char key) => key switch
    {
        >= '1' and <= '9' => key - '1',
        >= 'a' and <= 'z' => 9 + key - 'a',
        >= 'A' and <= 'Z' => 9 + key - 'A',
        _ => -1
    };
}
