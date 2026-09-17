using System.Text.Json.Serialization;

namespace Servitor.App.RulesData.Mapping.RulesText;

public sealed class RtFactionDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("detachments")]
    public List<RtDetachment> Detachments { get; set; } = [];
}

public sealed class RtDetachment
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("abilities")]
    public List<RtAbilityEntry> Abilities { get; set; } = [];

    [JsonPropertyName("enhancements")]
    public List<RtEnhancementEntry> Enhancements { get; set; } = [];

    [JsonPropertyName("stratagems")]
    public List<RtStratagemEntry> Stratagems { get; set; } = [];
}
