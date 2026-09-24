using System.Text.Json.Serialization;

namespace Servitor.RulesData.Mapping.BsData;

/// <summary>
/// A single BattleScribe node. The same shape is reused by the source format for units, models, weapons/wargear
/// ("upgrade"), entry links and selection-entry groups — they differ only in which optional fields are populated,
/// so one recursive DTO mirrors the source rather than one type per BattleScribe node kind.
/// </summary>
public sealed class BsEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("targetId")]
    public string? TargetId { get; set; }

    [JsonPropertyName("categoryLinks")]
    public List<BsCategoryLink> CategoryLinks { get; set; } = [];

    [JsonPropertyName("constraints")]
    public List<BsConstraint> Constraints { get; set; } = [];

    [JsonPropertyName("costs")]
    public List<BsCost> Costs { get; set; } = [];

    [JsonPropertyName("profiles")]
    public List<BsProfile> Profiles { get; set; } = [];

    [JsonPropertyName("infoLinks")]
    public List<BsInfoLink> InfoLinks { get; set; } = [];

    [JsonPropertyName("modifiers")]
    public List<BsModifier> Modifiers { get; set; } = [];

    [JsonPropertyName("entryLinks")]
    public List<BsEntry> EntryLinks { get; set; } = [];

    [JsonPropertyName("selectionEntries")]
    public List<BsEntry> SelectionEntries { get; set; } = [];

    [JsonPropertyName("selectionEntryGroups")]
    public List<BsEntry> SelectionEntryGroups { get; set; } = [];
}
