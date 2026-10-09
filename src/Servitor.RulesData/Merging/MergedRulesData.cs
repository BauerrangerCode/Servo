using Servitor.RulesData.Domain;

namespace Servitor.RulesData.Merging;

/// <summary>
/// The merged single source of truth the app reads: BSData datasheets (with the rules-text source's stratagem ids
/// attached), all stratagems, and the core rules of both sources. Never overwrites the raw source files.
/// <paramref name="DataVersion"/> identifies the source versions it was built from;
/// <paramref name="GeneratedAtUtc"/> orders two merge results of the same sources by recency.
/// </summary>
public sealed record MergedRulesData(
    string DataVersion,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<Datasheet> Datasheets,
    IReadOnlyList<Stratagem> CoreStratagems,
    IReadOnlyList<MergedFactionRules> Factions,
    IReadOnlyList<CoreRule> CoreRules);

public sealed record MergedFactionRules(
    string FactionId,
    string Name,
    IReadOnlyList<Stratagem> Stratagems,
    IReadOnlyList<DetachmentAbility> Abilities,
    IReadOnlyList<Enhancement> Enhancements,
    IReadOnlyList<Detachment> Detachments)
{
    /// <summary>The rules-text faction slugs whose units an army of this faction may take as allies.</summary>
    public IReadOnlyList<string> AllyFactionIds { get; init; } = [];
}

/// <summary>
/// The small header stored next to the (large) merged data so a consumer can decide which copy is newer without
/// loading either.
/// </summary>
public sealed record MergedDataManifest(string DataVersion, DateTimeOffset GeneratedAtUtc);
