using System.Text.Json.Serialization;

namespace Servitor.RulesData.Mapping.BsData;

public sealed class BsGameSystemDocument
{
    [JsonPropertyName("gameSystem")]
    public BsGameSystem? GameSystem { get; set; }
}

public sealed class BsGameSystem
{
    [JsonPropertyName("sharedRules")]
    public List<BsRule> SharedRules { get; set; } = [];
}

public sealed class BsRule
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}
