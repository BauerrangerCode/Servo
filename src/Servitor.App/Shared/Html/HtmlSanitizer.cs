using System.Text.RegularExpressions;

namespace Servitor.App.Shared.Html;

/// <summary>
/// Normalizes the inline HTML markup found in rules-text description fields (WHEN/TARGET/EFFECT formatting,
/// <c>&lt;b&gt;</c>/<c>&lt;span&gt;</c> tags) into a safe, allow-listed subset a Blazor MarkupString can render,
/// instead of passing the source's raw markup through untrusted.
/// </summary>
public static partial class HtmlSanitizer
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "b", "i", "u", "br", "div", "ul", "li", "span",
    };

    public static string Sanitize(string rawHtml)
    {
        if (string.IsNullOrEmpty(rawHtml))
        {
            return string.Empty;
        }

        var withoutScripts = ScriptOrStyleBlock().Replace(rawHtml, string.Empty);
        return TagPattern().Replace(withoutScripts, ReplaceTag);
    }

    private static string ReplaceTag(Match match)
    {
        var isClosing = match.Groups["closing"].Value == "/";
        var tagName = match.Groups["name"].Value.ToLowerInvariant();

        if (!AllowedTags.Contains(tagName))
        {
            return string.Empty;
        }

        if (isClosing)
        {
            return $"</{tagName}>";
        }

        if (tagName == "br")
        {
            return "<br />";
        }

        if (tagName == "span")
        {
            var classMatch = ClassAttributePattern().Match(match.Groups["attrs"].Value);
            if (classMatch.Success)
            {
                var safeClass = UnsafeClassCharacters().Replace(classMatch.Groups["value"].Value, string.Empty);
                if (safeClass.Length > 0)
                {
                    return $"<span class=\"{safeClass}\">";
                }
            }

            return "<span>";
        }

        return $"<{tagName}>";
    }

    [GeneratedRegex(@"<(script|style)\b[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptOrStyleBlock();

    [GeneratedRegex(@"<(?<closing>/?)(?<name>[a-zA-Z][a-zA-Z0-9]*)(?<attrs>[^>]*)>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"class\s*=\s*""(?<value>[^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex ClassAttributePattern();

    [GeneratedRegex(@"[^a-zA-Z0-9 _-]")]
    private static partial Regex UnsafeClassCharacters();
}
