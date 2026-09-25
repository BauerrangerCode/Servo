using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.ArmyBuilder.Catalog;

public sealed record CatalogGroup(UnitRole Role, IReadOnlyList<Datasheet> Units);

/// <summary>
/// The unit catalog of one army: which datasheets it can take and how they are grouped and filtered. Pure logic,
/// kept out of the components so it is testable without rendering.
/// </summary>
public static class UnitCatalog
{
    /// <summary>The datasheets the faction can take, either the current or the Legends ones, ordered by name.</summary>
    public static IReadOnlyList<Datasheet> ForFaction(IEnumerable<Datasheet> all, string factionSlug, bool showLegends) =>
        all.Where(d => d.FactionSlugs.Contains(factionSlug) && NameNormalizer.IsLegends(d.Name) == showLegends)
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Groups the units by role (in <see cref="UnitRoles.All"/> order, empty groups left out), optionally narrowed to one role and to names containing the query.</summary>
    public static IReadOnlyList<CatalogGroup> Group(IEnumerable<Datasheet> units, string query, UnitRole? role)
    {
        var matching = units.Where(d => string.IsNullOrWhiteSpace(query) || d.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));

        return matching
            .GroupBy(UnitRoles.Of)
            .Where(g => role is null || g.Key == role)
            .OrderBy(g => g.Key)
            .Select(g => new CatalogGroup(g.Key, g.ToList()))
            .ToList();
    }
}
