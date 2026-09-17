using Servitor.App.RulesData.Domain;

namespace Servitor.App.RulesData.Storage;

/// <summary>
/// Everything <see cref="IRulesDataStore"/> persists for one faction: both E1.3's BSData-mapped datasheets and
/// E1.3b's rules-text content, under the same per-faction key — not two parallel stores with separate versioning.
/// </summary>
public sealed record FactionRulesData(
    string FactionId,
    string DataVersion,
    IReadOnlyList<Datasheet> Datasheets,
    IReadOnlyList<Stratagem> Stratagems,
    IReadOnlyList<DetachmentAbility> Abilities,
    IReadOnlyList<Enhancement> Enhancements);
