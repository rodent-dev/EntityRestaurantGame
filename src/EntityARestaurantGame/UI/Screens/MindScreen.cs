using Game.Contracts;
using Game.Services;

namespace EntityARestaurantGame.UI.Screens;

/// <summary>
/// Debug window into an entity's interiority: the valuation sheet, current
/// needs (the operator turned inward), accumulated beliefs, and desire
/// bookmarks with their want recomputed live — pinned obsessions flagged.
/// </summary>
public class MindScreen : IScreen
{
    private readonly Entity _entity;
    private readonly EntityController _controller;

    public MindScreen(Entity entity, EntityController controller)
    {
        _entity = entity;
        _controller = controller;
    }

    public string Title => $"Mind: {_entity.DisplayName}";

    public void Render(ConsoleRenderer renderer)
    {
        renderer.DrawStatusBar($"Tick: {_controller.CurrentTick}  |  {Title}");

        var minds = _controller.Dialog?.Minds;
        var mind = _entity.Mind;
        var lines = new List<string>();

        lines.Add($"Traits: skepticism {mind.Skepticism:0.00}   impulsiveness {mind.Impulsiveness:0.00}");
        lines.Add(string.Empty);

        lines.Add("── Preferences (axis, weight) ──");
        if (mind.Preferences.Count == 0)
            lines.Add("  (indifferent to everything)");
        else
            foreach (var (axis, w) in mind.Preferences.OrderByDescending(p => p.Value))
                lines.Add($"  {axis,-12} {w:0.#}");

        if (minds != null)
        {
            lines.Add(string.Empty);
            lines.Add("── Needs (weight × shortfall, recomputed) ──");
            var needs = minds.NeedsOf(_entity).OrderByDescending(n => n.Shortfall).ToList();
            if (needs.Count == 0)
                lines.Add("  (content)");
            else
                foreach (var (axis, shortfall) in needs)
                    lines.Add($"  {axis,-12} {shortfall:0.00}");
        }

        lines.Add(string.Empty);
        lines.Add("── Desires (referent stored, want recomputed) ──");
        if (mind.Desires.Count == 0)
            lines.Add("  (wants nothing it has heard of)");
        else
            foreach (var d in mind.Desires)
            {
                string want = "gone";
                var found = FindItem(d.ObjectId);
                if (found != null && minds != null)
                    want = minds.EffectiveWant(_entity, d.ObjectId, found.Descriptors).ToString("0.00");
                string pin = d.PinnedWant > 0 ? $"  ★OBSESSED (pin {d.PinnedWant:0.00})" : "";
                lines.Add($"  {d.ObjectName,-16} want {want}  ({d.Context}, t{d.FirstSurfacedTick}){pin}");
            }

        lines.Add(string.Empty);
        lines.Add("── Beliefs (accepted claims, summed per axis) ──");
        if (mind.Beliefs.Count == 0)
            lines.Add("  (believes nothing in particular)");
        else
            foreach (var (objectId, sheet) in mind.Beliefs)
            {
                string entries = string.Join("  ", sheet
                    .OrderByDescending(kv => Math.Abs(kv.Value))
                    .Select(kv => $"{kv.Key} {kv.Value:+0.00;-0.00}"));
                lines.Add($"  {ResolveName(objectId),-14} {entries}");
            }

        if (mind.Deposited is { } dep)
        {
            lines.Add(string.Empty);
            lines.Add($"── Deposited (t{mind.DepositedTick}) ──");
            lines.Add($"  mulling over {dep.SpeakerName}'s {dep.Type} about {ResolveName(dep.TopicId)}");
        }

        renderer.DrawPanel(Title, lines);

        renderer.DrawOptions(
        [
            new MenuOption
            {
                Label = "Whisper a Rumor to This Entity",
                IsEnabled = _controller.Dialog != null,
                Execute = () => ScreenAction.Push(new RumorScreen(_entity, _controller))
            }
        ]);
        renderer.WriteLine();
        renderer.WriteLine("  [Esc] Back");
    }

    public ScreenAction HandleInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape) return ScreenAction.Pop;
        if (key.KeyChar == '1' && _controller.Dialog != null)
            return ScreenAction.Push(new RumorScreen(_entity, _controller));
        return ScreenAction.Stay;
    }

    private IItem? FindItem(string objectId) =>
        _controller.ActiveEntities
            .SelectMany(e => e.Inventory)
            .FirstOrDefault(i => i.ItemId == objectId);

    private string ResolveName(string objectId)
    {
        var entity = _controller.ActiveEntities.FirstOrDefault(e => e.ID == objectId);
        if (entity != null) return entity.DisplayName;
        return FindItem(objectId)?.ItemName ?? objectId;
    }
}
