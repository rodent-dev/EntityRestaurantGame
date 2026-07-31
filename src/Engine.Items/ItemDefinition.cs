using System.Text.Json.Serialization;

namespace Engine.Items;

/// <summary>
/// Plain data object deserialized from a JSON item file. Unknown JSON
/// properties are a load-time error — a typo'd field should fail loudly,
/// not silently deserialize to nothing.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class ItemDefinition
{
    [JsonPropertyName("itemId")]        public string ItemId { get; set; } = string.Empty;
    [JsonPropertyName("itemName")]      public string ItemName { get; set; } = string.Empty;
    [JsonPropertyName("description")]   public string Description { get; set; } = string.Empty;
    [JsonPropertyName("type")]          public string Type { get; set; } = "Standard";
    [JsonPropertyName("weight")]        public float Weight { get; set; }
    [JsonPropertyName("uses")]          public int Uses { get; set; } = -1;
    [JsonPropertyName("enablesActions")] public List<string> EnablesActions { get; set; } = new();
    [JsonPropertyName("descriptors")]   public Dictionary<string, double> Descriptors { get; set; } = new();
    [JsonPropertyName("effects")]       public List<EffectDefinition> Effects { get; set; } = new();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class EffectDefinition
{
    [JsonPropertyName("type")]       public string Type { get; set; } = string.Empty;
    [JsonPropertyName("trigger")]    public string Trigger { get; set; } = string.Empty;
    [JsonPropertyName("parameters")] public Dictionary<string, float> Parameters { get; set; } = new();
}
