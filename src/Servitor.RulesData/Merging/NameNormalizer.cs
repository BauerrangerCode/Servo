using System.Text.RegularExpressions;

namespace Servitor.RulesData.Merging;

/// <summary>
/// Builds the join key between the two sources' names. BSData marks Legends units with a "[Legends]" name
/// suffix while the rules-text source uses a separate flag, and the two differ in case and apostrophe style.
/// </summary>
public static partial class NameNormalizer
{
    private const string LegendsSuffix = "[Legends]";

    public static bool IsLegends(string bsDataName) =>
        bsDataName.Contains(LegendsSuffix, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string name)
    {
        var lowered = name.Replace(LegendsSuffix, "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        var withoutApostrophes = lowered.Replace("’", "").Replace("'", "");
        var lettersAndDigits = NonAlphanumeric().Replace(withoutApostrophes, " ");
        return Whitespace().Replace(lettersAndDigits, " ").Trim();
    }

    /// <summary>The normalized name without spaces, so "Counter-Offensive" and "Counteroffensive" share one key.</summary>
    public static string Compact(string name) => Normalize(name).Replace(" ", "");

    [GeneratedRegex(@"[^a-z0-9]")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
