using Servitor.RulesData.Domain;

namespace Servitor.App.RulesData.Search;

/// <summary>
/// Pure search/filter logic over an in-memory datasheet pool, kept out of any .razor file per
/// CODING_GUIDELINES.md so it is unit-testable without rendering.
/// </summary>
public static class DatasheetSearchService
{
    private const string LegendsMarker = "[Legends]";

    public static List<Datasheet> Search(IEnumerable<Datasheet> datasheets, DatasheetSearchCriteria criteria) =>
        datasheets
            .Where(d => MatchesLegends(d, criteria.ShowLegends))
            .Where(d => MatchesQuery(d, criteria.Query, criteria.Mode))
            .Where(d => MatchesFacet([d.Faction], criteria.FactionFilters))
            .Where(d => MatchesFacet(d.Keywords, criteria.RoleFilters))
            .Where(d => MatchesCharacteristic(d, criteria.CharacteristicFilter))
            .ToList();

    private static bool MatchesLegends(Datasheet datasheet, bool showLegends) =>
        datasheet.Name.Contains(LegendsMarker, StringComparison.OrdinalIgnoreCase) == showLegends;

    private static bool MatchesQuery(Datasheet datasheet, string query, DatasheetSearchMode mode)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var matchesName = datasheet.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
        if (mode == DatasheetSearchMode.Name)
        {
            return matchesName;
        }

        var matchesRulesText = MatchesRulesText(datasheet, query);
        return mode == DatasheetSearchMode.RulesText ? matchesRulesText : matchesName || matchesRulesText;
    }

    private static bool MatchesRulesText(Datasheet datasheet, string query)
    {
        bool Contains(string text) => text.Contains(query, StringComparison.OrdinalIgnoreCase);
        bool AbilityMatches(Ability ability) => Contains(ability.Name) || Contains(ability.Description);
        bool WeaponMatches(WeaponProfile weapon) => Contains(weapon.Name) || weapon.Keywords.Any(Contains);

        return datasheet.Keywords.Any(Contains)
            || datasheet.NamedAbilities.Any(AbilityMatches)
            || datasheet.WargearAbilities.Any(AbilityMatches)
            || datasheet.CoreAbilities.Any(AbilityMatches)
            || datasheet.RangedWeapons.Any(WeaponMatches)
            || datasheet.MeleeWeapons.Any(WeaponMatches);
    }

    private static bool MatchesFacet(IReadOnlyList<string> values, IReadOnlyDictionary<string, TriState> filters)
    {
        var excluded = filters.Where(f => f.Value == TriState.Exclude).Select(f => f.Key).ToList();
        if (excluded.Count > 0 && values.Any(v => excluded.Contains(v, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        var included = filters.Where(f => f.Value == TriState.Include).Select(f => f.Key).ToList();
        return included.Count == 0 || values.Any(v => included.Contains(v, StringComparer.OrdinalIgnoreCase));
    }

    private static bool MatchesCharacteristic(Datasheet datasheet, CharacteristicFilter? filter)
    {
        if (filter is null)
        {
            return true;
        }

        var rawValue = filter.Kind switch
        {
            CharacteristicKind.Toughness => datasheet.StatLine.Toughness,
            CharacteristicKind.Wounds => datasheet.StatLine.Wounds,
            _ => null,
        };

        return TryParseLeadingInt(rawValue) is { } value && value >= filter.MinimumValue;
    }

    private static int? TryParseLeadingInt(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        var digits = new string(raw.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : null;
    }
}
