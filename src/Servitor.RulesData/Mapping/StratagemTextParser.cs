using System.Text.RegularExpressions;

namespace Servitor.RulesData.Mapping;

public sealed record StratagemText(string When, string Target, string Effect, string Restrictions, string AdditionalText);

/// <summary>
/// Splits a stratagem's Markdown description into its labelled blocks. The rules-text source formats every block as
/// a bold, upper-case, colon-terminated label at the start of a line (<c>**WHEN:** …</c>).
/// </summary>
public static partial class StratagemTextParser
{
    private const string WhenLabel = "WHEN";
    private const string TargetLabel = "TARGET";
    private const string EffectLabel = "EFFECT";
    private const string RestrictionsLabel = "RESTRICTIONS";

    public static StratagemText Parse(string markdown)
    {
        var labels = LabelPattern().Matches(markdown);
        if (labels.Count == 0)
        {
            return new StratagemText("", "", "", "", markdown.Trim());
        }

        string when = "", target = "", effect = "", restrictions = "";
        var additional = new List<string>();

        var preamble = markdown[..labels[0].Index].Trim();
        if (preamble.Length > 0)
        {
            additional.Add(preamble);
        }

        for (var i = 0; i < labels.Count; i++)
        {
            var bodyStart = labels[i].Index + labels[i].Length;
            var bodyEnd = i + 1 < labels.Count ? labels[i + 1].Index : markdown.Length;
            var body = markdown[bodyStart..bodyEnd].Trim();
            var label = labels[i].Groups["label"].Value;

            switch (label)
            {
                case WhenLabel:
                    when = body;
                    break;
                case TargetLabel:
                    target = body;
                    break;
                case EffectLabel:
                    effect = body;
                    break;
                case RestrictionsLabel:
                    restrictions = body;
                    break;
                default:
                    additional.Add($"**{label}:**{SeparatorBefore(body)}{body}".TrimEnd());
                    break;
            }
        }

        return new StratagemText(when, target, effect, restrictions, string.Join("\n\n", additional));
    }

    // A list or table has to start on its own line to stay Markdown.
    private static string SeparatorBefore(string body) => BlockStart().IsMatch(body) ? "\n" : " ";

    [GeneratedRegex(@"^(-|\d+\.) |^\|")]
    private static partial Regex BlockStart();

    [GeneratedRegex(@"^\*\*(?<label>[A-Z][A-Z ]*?):\*\*", RegexOptions.Multiline)]
    private static partial Regex LabelPattern();
}
