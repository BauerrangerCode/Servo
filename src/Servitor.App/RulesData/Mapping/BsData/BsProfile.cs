using System.Text.Json.Serialization;

namespace Servitor.App.RulesData.Mapping.BsData;

public sealed class BsProfile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("typeName")]
    public string? TypeName { get; set; }

    [JsonPropertyName("characteristics")]
    public List<BsCharacteristic> Characteristics { get; set; } = [];
}

public sealed class BsCharacteristic
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("$text")]
    public string? Text { get; set; }
}
