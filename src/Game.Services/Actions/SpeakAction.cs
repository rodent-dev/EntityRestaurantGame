using Game.Contracts;

namespace Game.Services.Actions;

public class SpeakAction : IAction
{
    public string ActionName => "Speak";
    /// <summary><c>Persuasive</c> marks this as a pitch made WITH something —
    /// the distinguishing keyword that lets a whisper name it rather than the
    /// general Converse. See RecruitAction for why the three social verbs each
    /// needed one.</summary>
    public HashSet<string> Keywords { get; } = ["Social", "Diplomatic", "Persuasive"];

    public bool RequiresItem => true;

    public bool CanActorUse(Entity user) => user.Inventory.Any(i => i.CanEnableAction("Speak", user, user));
    public bool CanTarget(Entity user, Entity target) => user.ID != target.ID;

    public List<IItem> GetViableItems(Entity user, Entity target) =>
        user.Inventory.Where(i => i.CanEnableAction("Speak", user, target)).ToList();

    public void Execute(Entity user, Entity target, IItem? item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Apply("Speak", user, target);

        // Transfer the item to the target if it's single-use (gifting it)
        if (item.UsesRemaining == 0)
        {
            user.Inventory.Remove(item);
            target.Inventory.Add(item);
        }
    }
}
