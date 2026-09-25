using System.Globalization;
using System.Text.RegularExpressions;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Mapping.RulesText;

namespace Servitor.RulesData.Mapping;

/// <summary>
/// Reads a datasheet's cost rows ("10 models" = 90 pts, "20 models" = 180 pts) into <see cref="UnitSize"/>s. Rows
/// come in cost brackets by how many copies of the unit are already in the army; only the first bracket (the price
/// of the first copy) is read. Per-wargear rows ("per Multi-melta") are not sizes and are skipped.
/// </summary>
public static partial class UnitSizeParser
{
    private const string WargearSection = "WARGEAR OPTIONS";

    public static IReadOnlyList<UnitSize> Parse(IEnumerable<RtModelCost> rows)
    {
        var candidates = rows
            .Where(r => !string.Equals(r.Section, WargearSection, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var firstBracket = candidates.FirstOrDefault()?.Section;

        var sizes = new List<UnitSize>();
        foreach (var row in candidates.Where(r => r.Section == firstBracket))
        {
            if (!int.TryParse(row.Cost, NumberStyles.None, CultureInfo.InvariantCulture, out var points))
            {
                continue;
            }

            var models = CountModels(row.Description);
            if (models > 0 && sizes.All(s => s.Models != models))
            {
                sizes.Add(new UnitSize(models, points));
            }
        }

        return sizes.OrderBy(s => s.Models).ToList();
    }

    // "5 models" -> 5, "3 Wolf Guard Headtakers, 3 Hunting Wolves" -> 6, "per Lascannon" -> 0.
    private static int CountModels(string description) =>
        description.Split(',').Sum(part => LeadingCountRegex().Match(part.Trim()) is { Success: true } match
            ? int.Parse(match.Groups["count"].Value, CultureInfo.InvariantCulture)
            : 0);

    [GeneratedRegex(@"^(?<count>\d+)\s")]
    private static partial Regex LeadingCountRegex();
}
