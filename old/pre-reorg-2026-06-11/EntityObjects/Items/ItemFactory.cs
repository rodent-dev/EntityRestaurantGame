using System.Text.Json;
using EntityObjects.Definitions;
using EntityObjects.Effects;
using EntityObjects.Interfaces;

namespace EntityObjects.Items;

/// <summary>
/// Loads IItem instances from JSON files.
/// Priority: handlerClass (if specified) → DataDrivenItem.
/// </summary>
public class ItemFactory
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly EffectRegistry _registry;

    public ItemFactory(EffectRegistry registry) => _registry = registry;

    public IItem LoadFromFile(string path)
    {
        string json = File.ReadAllText(path);
        var def = JsonSerializer.Deserialize<ItemDefinition>(json, _jsonOptions)
            ?? throw new InvalidDataException($"Failed to deserialize item from '{path}'.");
        return Create(def);
    }

    public IReadOnlyList<IItem> LoadDirectory(string dirPath) =>
        Directory.GetFiles(dirPath, "*.json")
            .Select(LoadFromFile)
            .ToList();

    public IItem Create(ItemDefinition def)
    {
        if (def.HandlerClass != null)
        {
            var type = Type.GetType(def.HandlerClass)
                ?? throw new InvalidOperationException(
                    $"Handler class '{def.HandlerClass}' not found. Use a fully-qualified type name.");
            return (IItem)(Activator.CreateInstance(type, def)
                ?? throw new InvalidOperationException($"Could not create instance of '{def.HandlerClass}'."));
        }

        ValidateEffects(def);
        return new DataDrivenItem(def, _registry);
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
