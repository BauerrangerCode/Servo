using System.Text.Json.Serialization;

namespace Servitor.RulesData.Mapping.BsData;

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

    [JsonPropertyName("sharedSelectionEntryGroups")]
    public List<BsEntry> SharedSelectionEntryGroups { get; set; } = [];

    [JsonPropertyName("sharedProfiles")]
    public List<BsProfile> SharedProfiles { get; set; } = [];
}
