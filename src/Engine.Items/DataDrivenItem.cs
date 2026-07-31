namespace Engine.Items;

/// <summary>
/// An IItem backed by a JSON ItemDefinition. Delegates all behavior through
/// EffectRegistry. Used for any item that doesn't require a custom handler class.
/// </summary>
public class DataDrivenItem<TActor> : IItem<TActor>
{
    private readonly ItemDefinition _def;
    private readonly EffectRegistry<TActor> _registry;
    private bool _shouldConsume;

    public DataDrivenItem(ItemDefinition def, EffectRegistry<TActor> registry)
    {
        _def = def;
        _registry = registry;
        UsesRemaining = def.Uses;
    }

    public string ItemId => _def.ItemId;
    public string ItemName => _def.ItemName;
    public string Description => _def.Description;
    public float Weight => _def.Weight;
    public int UsesRemaining { get; set; }
    public IReadOnlyDictionary<string, double> Descriptors => _def.Descriptors;

    public bool CanEnableAction(string actionName, TActor user, TActor target) =>
        UsesRemaining != 0 && _def.EnablesActions.Contains(actionName);

    public void Apply(string actionName, TActor user, TActor target)
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
