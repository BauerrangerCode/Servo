namespace Servitor.RulesData.Merging;

/// <summary>A BSData catalogue file with its own id and the ids of the catalogues it links to.</summary>
public sealed record CatalogueLinks(string FileName, string Id, IReadOnlyList<string> LinkedIds);

/// <summary>
/// Works out which armies may take allies from which other armies. BSData lets a faction's catalogue import the units
/// of other catalogues; those imports are the allies. A linked catalogue only counts when it maps to factions other than
/// the army's own, and to exactly one faction: an army's own library, the shared Aeldari library and the Legends
/// library (which holds the units of several factions) are not allies.
/// </summary>
public static class AllyResolver
{
    /// <returns>For each army faction slug the slugs of the factions it may take allies from, in the order of the source.</returns>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Resolve(IReadOnlyList<CatalogueLinks> catalogues)
    {
        var slugsById = catalogues
            .Where(c => c.Id.Length > 0)
            .ToDictionary(c => c.Id, c => FactionCatalogueMap.FactionSlugsFor(c.FileName), StringComparer.Ordinal);

        var allies = new Dictionary<string, List<string>>();
        foreach (var catalogue in catalogues)
        {
            var own = FactionCatalogueMap.FactionSlugsFor(catalogue.FileName);
            if (own.Count == 0)
            {
                continue;
            }

            foreach (var linkedId in catalogue.LinkedIds)
            {
                if (!slugsById.TryGetValue(linkedId, out var linked) || linked.Count != 1 || own.Contains(linked[0]))
                {
                    continue;
                }

                foreach (var army in own)
                {
                    if (!allies.TryGetValue(army, out var list))
                    {
                        allies[army] = list = [];
                    }

                    list.AddRange(linked.Where(slug => !list.Contains(slug)));
                }
            }
        }

        return allies.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value);
    }
}
