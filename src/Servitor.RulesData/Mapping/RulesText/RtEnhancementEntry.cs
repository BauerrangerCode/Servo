using System.Text.Json.Serialization;

namespace Servitor.RulesData.Mapping.RulesText;

public sealed class RtEnhancementEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("legend")]
    public string Legend { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("cost")]
    public string Cost { get; set; } = "";

    [JsonPropertyName("detachment")]
    public string Detachment { get; set; } = "";

    [JsonPropertyName("upgrade")]
    public bool Upgrade { get; set; }

    [JsonPropertyName("supportLeader")]
    public string? SupportLeader { get; set; }
}
