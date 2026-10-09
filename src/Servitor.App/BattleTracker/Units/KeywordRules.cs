using System.Text.RegularExpressions;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.BattleTracker.Units;

/// <summary>
/// Finds the core rule behind a keyword shown on a unit or a weapon ("Fly", "Lethal Hits", "Rapid Fire 2",
/// "Anti-Infantry 4+"). The keyword is tried as written, then without its value or note, then up to its first hyphen,
/// so a rule with a parameter still finds its rule. A few unit keywords are mapped to the section that explains them. A keyword without a core rule of that name has no rule to show.
/// </summary>
public sealed partial class KeywordRules(IReadOnlyList<CoreRule> rules)
{
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
        foreach (var candidate in Candidates(keyword))
        {
            var key = NameNormalizer.Normalize(candidate);
            if (key.Length == 0)
            {
                continue;
            }

            var match = rules.Where(r => NameNormalizer.Normalize(r.Name) == key)
                .OrderBy(r => r.Kind is CoreRuleKind.WeaponRule or CoreRuleKind.UnitRule ? 0 : 1)
                .FirstOrDefault();
            if (match is not null)
            {
                return match;
            }
        }

        return null;
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
