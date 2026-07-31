using Game.Contracts;

namespace Game.Services.Actions;

/// <summary>
/// The talk beat. Selecting Converse in the action selector hands the beat to
/// DialogService's utterance selector — dialog surfaces objects, desire
/// accrues, and the same selector flips into the exchange path on its own.
/// Surfacing and transacting are different beats.
/// </summary>
public class ConverseAction : IAction
{
    private readonly DialogService _dialog;

    public ConverseAction(DialogService dialog) => _dialog = dialog;

    public string ActionName => "Converse";
    public HashSet<string> Keywords { get; } = ["Social", "Diplomatic"];
    public bool RequiresItem => false;
    public int BaseWeight => 2;

    public bool CanActorUse(Entity user) => true;

    // Any living creature is reachable — comprehension decides what LANDS,
    // not whether you may try (design rule: never hard-wall an interaction
    // channel; vary the flavor). Talking to a wolf is tone, not language.
    public bool CanTarget(Entity user, Entity target) => user.ID != target.ID && target.IsAlive;
    public List<IItem> GetViableItems(Entity user, Entity target) => [];

    public void Execute(Entity user, Entity target, IItem? item) => _dialog.Converse(user, target);
}
