using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.ArmyBuilder.Catalog;

/// <summary>
/// Name-normalized catalog lookups shared by every resolver that turns an external reference (pasted Warscribe
/// text, a saved army) back into a rules-data object, so matching behavior — Unicode-insensitive comparison,
/// Legends-suffix handling — stays identical across all of them.
/// </summary>
public static class CatalogNameResolver
{
    public static T? FindByName<T>(IEnumerable<T> items, Func<T, string> nameSelector, string wantedName) where T : class =>
        items.FirstOrDefault(i => NameNormalizer.Normalize(nameSelector(i)) == NameNormalizer.Normalize(wantedName));

    /// <param name="datasheets">Candidates already scoped to the right faction (the army's own or an ally's).</param>
    public static (Datasheet Datasheet, UnitSize Size)? ResolveUnitPlan(
        string name, int modelCount, IReadOnlyList<Datasheet> datasheets, List<string> errors)
    {
        var candidates = datasheets.Where(d => NameNormalizer.Normalize(d.Name) == NameNormalizer.Normalize(name)).ToList();
        if (candidates.Count == 0)
        {
            errors.Add($"Unknown unit \"{name}\" for this faction.");
            return null;
        }

        // NameNormalizer strips the "[Legends]" suffix, so a Legends and non-Legends datasheet can share a
        // normalized name — the non-Legends one wins when both are candidates.
        var datasheet = candidates.FirstOrDefault(d => !d.Name.Contains("[Legends]", StringComparison.OrdinalIgnoreCase)) ?? candidates[0];

        var sizes = UnitSizes.For(datasheet);
        var size = sizes.FirstOrDefault(s => s.Models == modelCount)
            ?? (sizes is [{ Models: 0 } only] && modelCount == 1 ? only : null);

        if (size is null)
        {
            errors.Add($"\"{name}\" has no {modelCount}-model size in the catalog.");
            return null;
        }

        return (datasheet, size);
    }
}
