using System.Text.Json.Serialization;

namespace Servitor.RulesData.Mapping.RulesText;

public sealed class RtFactionDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("dataVersion")]
    public string DataVersion { get; set; } = "";

    [JsonPropertyName("datasheets")]
    public List<RtDatasheetSummary> Datasheets { get; set; } = [];

    [JsonPropertyName("detachments")]
    public List<RtDetachment> Detachments { get; set; } = [];
}

public sealed class RtDatasheetSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("isLegends")]
    public bool IsLegends { get; set; }
}

public sealed class RtDatasheetDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("stratagemIds")]
    public List<string> StratagemIds { get; set; } = [];
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
