using System.Net;
using System.Text.RegularExpressions;

namespace Servitor.App.BattleTracker.ActionQueue;

/// <summary>
/// Finds the named alternatives inside the HTML of a rule that says "select one of the following/listed below". The
/// source comes in two shapes: bullets that open with a bold name and a colon, and one block per alternative whose name
/// runs straight into its text because the markup between them was stripped ("Devastator DoctrineThe Codex ...").
/// Anything else, and any rule with fewer than two alternatives, is not a choice.
/// </summary>
public static partial class RuleOptionParser
{
    private const int MinimumOptions = 2;

    [GeneratedRegex(@"\b(select|choose)\b[^.]*\b(one|two)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ChoicePrompt();

    [GeneratedRegex(@"<li>\s*<b>(?<name>[^<]+?):?</b>:?\s*(?<text>.*?)</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex BulletOption();

    [GeneratedRegex(@"<div>(?<body>(?:(?!</?div>).)+)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex BlockOption();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"^(?<name>.+?[a-z])(?<text>[A-Z].+)$", RegexOptions.Singleline)]
    private static partial Regex RunTogetherNameAndText();

    public static IReadOnlyList<RuleOption> Parse(string html)
    {
        if (!ChoicePrompt().IsMatch(Clean(html)))
        {
            return [];
        }

        var bullets = BulletOption().Matches(html)
            .Select(m => new RuleOption(Clean(m.Groups["name"].Value), Clean(m.Groups["text"].Value)))
            .ToList();
        if (bullets.Count >= MinimumOptions)
        {
            return bullets;
        }

        var blocks = BlockOption().Matches(html)
            .Select(m => RunTogetherNameAndText().Match(Clean(m.Groups["body"].Value)))
            .Where(m => m.Success)
            .Select(m => new RuleOption(m.Groups["name"].Value.Trim(), m.Groups["text"].Value.Trim()))
            .ToList();
        return blocks.Count >= MinimumOptions ? blocks : [];
    }

    private static string Clean(string html) => WebUtility.HtmlDecode(Tag().Replace(html, "")).Trim();
}
