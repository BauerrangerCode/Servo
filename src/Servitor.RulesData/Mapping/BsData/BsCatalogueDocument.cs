using System.Text.Json.Serialization;

namespace Servitor.RulesData.Mapping.BsData;

public sealed class BsCatalogueDocument
{
    [JsonPropertyName("catalogue")]
    public BsCatalogue? Catalogue { get; set; }
}

public sealed class BsCatalogueLink
{
    [JsonPropertyName("targetId")]
    public string TargetId { get; set; } = "";
}

public sealed class BsCatalogue
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>The other catalogues this one imports units from; for an army these are the factions it may take allies from.</summary>
    [JsonPropertyName("catalogueLinks")]
    public List<BsCatalogueLink> CatalogueLinks { get; set; } = [];

    [JsonPropertyName("sharedSelectionEntries")]
    public List<BsEntry> SharedSelectionEntries { get; set; } = [];

    [JsonPropertyName("sharedSelectionEntryGroups")]
    public List<BsEntry> SharedSelectionEntryGroups { get; set; } = [];

    [JsonPropertyName("sharedProfiles")]
    public List<BsProfile> SharedProfiles { get; set; } = [];
}
