using System.Text.Json.Serialization;

namespace Servitor.App.RulesData.Mapping.RulesText;

public sealed class RtAbilityEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("legend")]
    public string Legend { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("detachment")]
    public string Detachment { get; set; } = "";
}
