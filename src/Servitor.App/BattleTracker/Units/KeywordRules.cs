using System.Text.RegularExpressions;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.BattleTracker.Units;

/// <summary>The rule text a keyword leads to: the rule's name and its Markdown, which for a whole chapter includes its sections.</summary>
public sealed record KeywordRule(string Name, string Markdown);

/// <summary>
/// Finds the core rule behind a keyword shown on a unit or a weapon ("Fly", "Lethal Hits", "Rapid Fire 2",
/// "Anti-Infantry 4+"). The keyword is tried as written, then without its value or note, then up to its first hyphen,
/// so a rule with a parameter still finds its rule. A few unit keywords are mapped to the chapter or section that
/// explains them. A keyword without a core rule of that name has no rule to show.
/// </summary>
public sealed partial class KeywordRules
{
    // Unit keywords whose rule the core rules name differently. A chapter alias also brings the numbered sections of the
    // chapter, since the chapter entry itself is only its introduction (the keyword Monster is explained in 17.01-17.03).
    private static readonly Dictionary<string, (string Rule, string? ChapterPrefix)> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Fly"] = ("Flying Models", null),
        ["Monster"] = ("Monsters and Vehicles", "17."),
        ["Vehicle"] = ("Monsters and Vehicles", "17."),
        ["Transport"] = ("Transports", "18."),
        ["Dedicated Transport"] = ("Transports", "18."),
    };

    private readonly IReadOnlyList<CoreRule> _rules;
    private readonly Dictionary<string, CoreRule> _byName;
    private readonly Dictionary<string, KeywordRule?> _found = [];

    public KeywordRules(IReadOnlyList<CoreRule> rules)
    {
        _rules = rules;

        // A weapon or unit rule wins over a general section of the same name.
        _byName = rules
            .GroupBy(r => NameNormalizer.Normalize(r.Name))
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Kind is CoreRuleKind.WeaponRule or CoreRuleKind.UnitRule ? 0 : 1).First());
    }

    public static KeywordRules Empty { get; } = new([]);

    [GeneratedRegex(@"\s+(\d+|D\d)\S*$", RegexOptions.IgnoreCase)]
    private static partial Regex Parameter();

    public KeywordRule? Find(string keyword)
    {
        if (_found.TryGetValue(keyword, out var known))
        {
            return known;
        }

        var rule = Candidates(keyword)
            .Select(NameNormalizer.Normalize)
            .Where(key => key.Length > 0 && _byName.ContainsKey(key))
            .Select(key => _byName[key])
            .FirstOrDefault();
        var found = rule is null ? null : Describe(rule, keyword);
        _found[keyword] = found;
        return found;
    }

    private KeywordRule Describe(CoreRule rule, string keyword)
    {
        var baseKeyword = Parameter().Replace(keyword.Split(':')[0].Trim().Trim('[', ']'), "");
        if (!Aliases.TryGetValue(baseKeyword, out var alias) || alias.ChapterPrefix is not { } prefix
            || NameNormalizer.Normalize(alias.Rule) != NameNormalizer.Normalize(rule.Name))
        {
            return new KeywordRule(rule.Name, rule.Markdown);
        }

        var sections = _rules
            .Where(r => r.Number is not null && r.Number.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(r => r.Number, StringComparer.Ordinal)
            .Select(r => $"### {r.Number} {r.Name}\n\n{r.Markdown}");
        return new KeywordRule(rule.Name, string.Join("\n\n", [rule.Markdown, .. sections]));
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
            yield return alias.Rule;
        }

        var hyphen = withoutValue.IndexOf('-');
        if (hyphen > 0)
        {
            yield return withoutValue[..hyphen];
        }
    }
}
