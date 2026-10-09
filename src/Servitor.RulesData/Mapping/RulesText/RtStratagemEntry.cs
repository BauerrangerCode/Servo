using System.Text.Json.Serialization;

namespace Servitor.RulesData.Mapping.RulesText;

public sealed class RtStratagemEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("cpCost")]
    public string CpCost { get; set; } = "";

    [JsonPropertyName("legend")]
    public string Legend { get; set; } = "";

    [JsonPropertyName("turn")]
    public string Turn { get; set; } = "";

    [JsonPropertyName("phase")]
    public string Phase { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("detachment")]
    public string Detachment { get; set; } = "";
}
