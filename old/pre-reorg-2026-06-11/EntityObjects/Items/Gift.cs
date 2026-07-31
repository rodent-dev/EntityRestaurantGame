using EntityObjects.Interfaces;

namespace EntityObjects.Items;

public class Gift : IItem
{
    public string ItemId => "gift";
    public string ItemName => "A Small Gift";
    public string Description => "A nicely wrapped gift.";
    public ItemType Type => ItemType.Merit;
    public float SpeakEffectModifier => 1.0f;
    public int UsesRemaining { get; set; } = 1;
    public bool IsEquipped { get; set; }
    public float Weight => 1f;

    public bool CanEnableAction(string actionName, Entity user, Entity target) => actionName == "Speak";
    public float GetEffectivenessScore(string actionName, Entity user, Entity target) => 1f;
    public float GetScarcityPenalty(Entity user) => user.Inventory.Count(i => i is Gift) <= 1 ? 0.5f : 0f;

    public void Apply(string actionName, Entity user, Entity target) { }
}
