using System.Text.Json.Serialization;

namespace EntityObjects.Definitions;

/// <summary>
/// Plain data object deserialized from a JSON item file.
/// See specs/001-entity-influence-engine/contracts/item-definition-schema.md for the full schema.
/// </summary>
public class ItemDefinition
{
    [JsonPropertyName("itemId")]        public string ItemId { get; set; } = string.Empty;
    [JsonPropertyName("itemName")]      public string ItemName { get; set; } = string.Empty;
    [JsonPropertyName("description")]   public string Description { get; set; } = string.Empty;
    [JsonPropertyName("type")]          public string Type { get; set; } = "Standard";
    [JsonPropertyName("weight")]        public float Weight { get; set; }
    [JsonPropertyName("uses")]          public int Uses { get; set; } = -1;
    [JsonPropertyName("isEquipped")]    public bool IsEquipped { get; set; }
    [JsonPropertyName("speakEffectModifier")] public float SpeakEffectModifier { get; set; }
    [JsonPropertyName("enablesActions")] public List<string> EnablesActions { get; set; } = new();
    [JsonPropertyName("effects")]       public List<EffectDefinition> Effects { get; set; } = new();
    [JsonPropertyName("handlerClass")]  public string? HandlerClass { get; set; }
}

public class EffectDefinition
{
    [JsonPropertyName("type")]       public string Type { get; set; } = string.Empty;
    [JsonPropertyName("trigger")]    public string Trigger { get; set; } = string.Empty;
    [JsonPropertyName("parameters")] public Dictionary<string, float> Parameters { get; set; } = new();
}
