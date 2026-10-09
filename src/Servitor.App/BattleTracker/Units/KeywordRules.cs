using System.Text.RegularExpressions;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.BattleTracker.Units;

/// <summary>
/// Finds the core rule behind a keyword shown on a unit or a weapon ("Fly", "Lethal Hits", "Rapid Fire 2",
/// "Anti-Infantry 4+"). The keyword is tried as written, then without its value or note, then up to its first hyphen,
/// so a rule with a parameter still finds its rule. A few unit keywords are mapped to the section that explains them. A keyword without a core rule of that name has no rule to show.
/// </summary>
public sealed partial class KeywordRules
{
    private readonly Dictionary<string, CoreRule> _byName;
    private readonly Dictionary<string, CoreRule?> _found = [];

    public KeywordRules(IReadOnlyList<CoreRule> rules)
    {
        // A weapon or unit rule wins over a general section of the same name.
        _byName = rules
            .GroupBy(r => NameNormalizer.Normalize(r.Name))
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Kind is CoreRuleKind.WeaponRule or CoreRuleKind.UnitRule ? 0 : 1).First());
    }

    public static KeywordRules Empty { get; } = new([]);

    // Unit keywords whose rule the core rules name differently (the keyword Fly is explained under "Flying Models").
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Fly"] = "Flying Models",
        ["Monster"] = "Monsters and Vehicles",
        ["Vehicle"] = "Monsters and Vehicles",
        ["Transport"] = "Transports",
        ["Dedicated Transport"] = "Transports",
    };

    [GeneratedRegex(@"\s+(\d+|D\d)\S*$", RegexOptions.IgnoreCase)]
    private static partial Regex Parameter();

    public CoreRule? Find(string keyword)
    {
        if (_found.TryGetValue(keyword, out var known))
        {
            return known;
        }

        var found = Candidates(keyword)
            .Select(NameNormalizer.Normalize)
            .Where(key => key.Length > 0 && _byName.ContainsKey(key))
            .Select(key => _byName[key])
            .FirstOrDefault();
        _found[keyword] = found;
        return found;
    }

    private static IEnumerable<string> Candidates(string keyword)
    {
        var text = keyword.Trim().Trim('[', ']');
        var beforeNote = text.Split(':')[0].Trim();
        var withoutValue = Parameter().Replace(beforeNote, "");
        yield return text;
        yield return beforeNote;
        yield return withoutValue;
        if (Aliases.TryGetValue(withoutValue, out var alias))
        {
            yield return alias;
        }

        var hyphen = withoutValue.IndexOf('-');
        if (hyphen > 0)
        {
            yield return withoutValue[..hyphen];
        }
    }
}
