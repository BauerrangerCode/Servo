using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.ArmyBuilder.Catalog;

/// <summary>Which Space Marines Chapter a unit belongs to.</summary>
public static class UnitChapters
{
    private const string ChapterCataloguePrefix = "Imperium - Adeptus Astartes - ";
    private const string GeneralCatalogue = "Space Marines";

    /// <summary>
    /// The Chapter named by the unit's catalogue ("Imperium - Adeptus Astartes - Blood Angels" gives "Blood Angels"), or
    /// null for units of the general Space Marines catalogue and of every other army.
    /// </summary>
    public static string? Of(Datasheet datasheet)
    {
        if (!datasheet.Faction.StartsWith(ChapterCataloguePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var chapter = datasheet.Faction[ChapterCataloguePrefix.Length..].Trim();
        return chapter.Equals(GeneralCatalogue, StringComparison.OrdinalIgnoreCase) ? null : chapter;
    }

    /// <summary>Chapter names are compared the way the merge compares names ("SPACE WOLVES" equals "Space Wolves").</summary>
    public static bool Same(string a, string b) => NameNormalizer.Normalize(a) == NameNormalizer.Normalize(b);
}
