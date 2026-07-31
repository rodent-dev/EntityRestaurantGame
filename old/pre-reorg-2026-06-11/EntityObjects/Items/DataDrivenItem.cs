using EntityObjects.Definitions;
using EntityObjects.Effects;
using EntityObjects.Interfaces;

namespace EntityObjects.Items;

/// <summary>
/// An IItem backed by a JSON ItemDefinition. Delegates all behavior through EffectRegistry.
/// Used for any item that doesn't require a custom handler class.
/// </summary>
public class DataDrivenItem : IItem
{
    private readonly ItemDefinition _def;
    private readonly EffectRegistry _registry;
    private bool _shouldConsume;

    public DataDrivenItem(ItemDefinition def, EffectRegistry registry)
    {
        _def = def;
        _registry = registry;
        UsesRemaining = def.Uses;
    }

    public string ItemId => _def.ItemId;
    public string ItemName => _def.ItemName;
    public string Description => _def.Description;
    public ItemType Type => Enum.Parse<ItemType>(_def.Type);
    public float SpeakEffectModifier => _def.SpeakEffectModifier;
    public int UsesRemaining { get; set; }
    public bool IsEquipped { get; set; }
    public float Weight => _def.Weight;

    public bool CanEnableAction(string actionName, Entity user, Entity target) =>
        UsesRemaining != 0 && _def.EnablesActions.Contains(actionName);

    public float GetEffectivenessScore(string actionName, Entity user, Entity target) => 1f;

    public float GetScarcityPenalty(Entity user) => 0f;

    public void Apply(string actionName, Entity user, Entity target)
    {
        _shouldConsume = false;

        foreach (var effect in _def.Effects.Where(e => e.Trigger == actionName))
        {
            if (effect.Type == "ConsumeItem")
            {
                _shouldConsume = true;
                continue;
            }
            _registry.Resolve(effect.Type).Apply(user, target, effect.Parameters);
        }

        if (_shouldConsume && UsesRemaining > 0)
            UsesRemaining--;
    }
}
