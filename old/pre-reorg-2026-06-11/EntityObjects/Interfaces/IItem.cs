namespace EntityObjects.Interfaces;

public interface IItem
{
    string ItemId { get; }
    string ItemName { get; }
    string Description { get; }
    ItemType Type { get; }
    float SpeakEffectModifier { get; }

    bool CanEnableAction(string actionName, Entity user, Entity target);
    float GetEffectivenessScore(string actionName, Entity user, Entity target);
    void Apply(string actionName, Entity user, Entity target);

    int UsesRemaining { get; set; }
    bool IsDepleted => UsesRemaining == 0;
    float GetScarcityPenalty(Entity user);

    bool IsEquipped { get; set; }
    float Weight { get; }
}

public enum ItemType
{
    Standard,
    Weapon,
    Consumable,
    Merit,
    Skill       // Inherent abilities — non-transferable, infinite-use, always equipped
}
