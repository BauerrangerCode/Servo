using System.Globalization;
using System.Text.RegularExpressions;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Mapping.RulesText;

namespace Servitor.RulesData.Mapping;

/// <summary>
/// Reads a datasheet's cost rows ("10 models" = 90 pts, "20 models" = 180 pts) into <see cref="CostBracket"/>s of
/// <see cref="UnitSize"/>s. The rows come in brackets by how many copies of the unit are already in the army
/// ("YOUR 1ST TO 2ND UNITS COST", "YOUR 3RD + UNIT COSTS"). Per-wargear rows ("per Multi-melta") are not sizes and
/// are skipped.
/// </summary>
public static partial class UnitSizeParser
{
    private const string WargearSection = "WARGEAR OPTIONS";

    /// <summary>The sizes of the first bracket, i.e. what the first copy of the unit costs.</summary>
    public static IReadOnlyList<UnitSize> Parse(IEnumerable<RtModelCost> rows) =>
        ParseBrackets(rows).FirstOrDefault()?.Sizes ?? [];

    /// <returns>The brackets in source order; brackets without a readable size are left out.</returns>
    public static IReadOnlyList<CostBracket> ParseBrackets(IEnumerable<RtModelCost> rows)
    {
        var sizeRows = rows
            .Where(r => !string.Equals(r.Section, WargearSection, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var brackets = new List<CostBracket>();
        foreach (var group in sizeRows.GroupBy(r => r.Section))
        {
            var sizes = ReadSizes(group);
            if (sizes.Count > 0)
            {
                var (first, last) = CopyRange(group.Key);
                brackets.Add(new CostBracket(first, last, sizes));
            }
        }

        return brackets;
    }

    private static List<UnitSize> ReadSizes(IEnumerable<RtModelCost> rows)
    {
        var sizes = new List<UnitSize>();
        foreach (var row in rows)
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

    // "YOUR 1ST TO 2ND UNITS COST" -> (1, 2), "YOUR 3RD + UNIT COSTS" -> (3, null), "YOUR UNIT COSTS" -> (1, null).
    private static (int First, int? Last) CopyRange(string? section)
    {
        var ordinals = OrdinalRegex().Matches(section ?? "").Select(m => int.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();
        if (ordinals.Count == 0)
        {
            return (1, null);
        }

        return (ordinals[0], section!.Contains('+') ? null : ordinals[^1]);
    }

    // "5 models" -> 5, "3 Wolf Guard Headtakers, 3 Hunting Wolves" -> 6, "per Lascannon" -> 0.
    private static int CountModels(string description) =>
        description.Split(',').Sum(part => LeadingCountRegex().Match(part.Trim()) is { Success: true } match
            ? int.Parse(match.Groups["count"].Value, CultureInfo.InvariantCulture)
            : 0);

    [GeneratedRegex(@"^(?<count>\d+)\s")]
    private static partial Regex LeadingCountRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex OrdinalRegex();
}
