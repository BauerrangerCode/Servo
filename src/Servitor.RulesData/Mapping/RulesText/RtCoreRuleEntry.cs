using System.Text.Json.Serialization;

namespace Servitor.RulesData.Mapping.RulesText;

public sealed class RtCoreRuleEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}
