using System.Text.Json;

namespace Engine.Items;

/// <summary>
/// Loads DataDrivenItem instances from JSON files, validating every effect
/// type against the registry at load time.
/// </summary>
public class ItemFactory<TActor>
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly EffectRegistry<TActor> _registry;

    public ItemFactory(EffectRegistry<TActor> registry) => _registry = registry;

    public IItem<TActor> LoadFromFile(string path)
    {
        string json = File.ReadAllText(path);
        var def = JsonSerializer.Deserialize<ItemDefinition>(json, _jsonOptions)
            ?? throw new InvalidDataException($"Failed to deserialize item from '{path}'.");
        return Create(def);
    }

    public IReadOnlyList<IItem<TActor>> LoadDirectory(string dirPath) =>
        Directory.GetFiles(dirPath, "*.json")
            .Select(LoadFromFile)
            .ToList();

    public IItem<TActor> Create(ItemDefinition def)
    {
        ValidateEffects(def);
        return new DataDrivenItem<TActor>(def, _registry);
    }

    private void ValidateEffects(ItemDefinition def)
    {
        foreach (var effect in def.Effects)
        {
            if (effect.Type == "ConsumeItem") continue;
            if (!_registry.IsRegistered(effect.Type))
                throw new InvalidOperationException(
                    $"Item '{def.ItemId}' references unregistered effect type '{effect.Type}'. " +
                    $"Register it before loading items.");
        }
    }
}
