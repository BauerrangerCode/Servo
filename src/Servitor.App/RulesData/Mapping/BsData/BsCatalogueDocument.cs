using System.Text.Json.Serialization;

namespace Servitor.App.RulesData.Mapping.BsData;

public sealed class BsCatalogueDocument
{
    [JsonPropertyName("catalogue")]
    public BsCatalogue? Catalogue { get; set; }
}

public sealed class BsCatalogue
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("sharedSelectionEntries")]
    public List<BsEntry> SharedSelectionEntries { get; set; } = [];

    [JsonPropertyName("sharedProfiles")]
    public List<BsProfile> SharedProfiles { get; set; } = [];
}
