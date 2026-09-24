using Servitor.RulesData.Domain;

namespace Servitor.RulesData.Merging;

public sealed record ConsolidationResult<T>(IReadOnlyList<T> Kept, IReadOnlyList<string> Dropped);

/// <summary>
/// The rules-text core file lists the same stratagem or action once per rules version and mode under different ids
/// (and with spelling variants such as "COUNTER-OFFENSIVE" / "COUNTEROFFENSIVE"). Of each such group the entry with the
/// highest id — the most recent rules version — is kept and the others are reported as dropped. Entries of different
/// game modes are never duplicates of each other.
/// </summary>
internal static class CoreDuplicateConsolidator
{
    public static ConsolidationResult<Stratagem> Stratagems(IReadOnlyList<Stratagem> stratagems) =>
        Consolidate(
            stratagems,
            s => $"{s.Mode}/{NameNormalizer.Compact(s.Name)}",
            s => s.Id,
            s => $"Stratagem {s.Name} ({s.Id}, {s.Mode})");

    public static ConsolidationResult<CoreRule> Actions(IReadOnlyList<CoreRule> actions) =>
        Consolidate(actions, a => NameNormalizer.Compact(a.Name), a => a.Id, a => $"Action {a.Name} ({a.Id})");

    private static ConsolidationResult<T> Consolidate<T>(
        IReadOnlyList<T> items, Func<T, string> groupKey, Func<T, string> idOf, Func<T, string> describe)
        where T : class
    {
        var newestByGroup = items
            .GroupBy(groupKey)
            .ToDictionary(g => g.Key, g => g.MaxBy(idOf, StringComparer.Ordinal)!);

        var kept = items.Where(item => ReferenceEquals(newestByGroup[groupKey(item)], item)).ToList();
        var dropped = items
            .Where(item => !ReferenceEquals(newestByGroup[groupKey(item)], item))
            .Select(item => $"{describe(item)} superseded by {idOf(newestByGroup[groupKey(item)])}")
            .ToList();

        return new ConsolidationResult<T>(kept, dropped);
    }
}
