using EntityObjects.Interfaces;

namespace EntityObjects.Items;

public class Fist : IItem
{
    public string ItemId => "fist";
    public string ItemName => "Fist";
    public string Description => "A bare-knuckled fist.";
    public ItemType Type => ItemType.Weapon;
    public float SpeakEffectModifier => 0f;
    public int UsesRemaining { get; set; } = -1;
    public bool IsEquipped { get; set; } = true;
    public float Weight => 0f;

    public bool CanEnableAction(string actionName, Entity user, Entity target) =>
        actionName is "Attack" or "Defend";

    public float GetEffectivenessScore(string actionName, Entity user, Entity target) => 1f;
    public float GetScarcityPenalty(Entity user) => 0f;

    public void Apply(string actionName, Entity user, Entity target)
    {
        if (actionName == "Attack")
            target.TakeDamage(5);
    }
}
