using System.Text.RegularExpressions;

namespace Servitor.DataUpdate.Wahapedia;

/// <summary>
/// Cleans the HTML inside Wahapedia CSV cells: drops the name/legend blocks that duplicate separate columns,
/// unwraps links and italics, strips presentation attributes and empty paragraphs. Equivalent to depot's
/// <c>convert-to-json.ts</c>, done with a small tag scanner instead of an HTML parser library.
/// </summary>
public static partial class WahapediaHtml
{
    public static string Strip(string input)
    {
        var html = CommentRegex().Replace(input, "");
        html = RemoveElements(html);
        html = UnwrapTagsRegex().Replace(html, "");
        html = RemovedAttributeRegex().Replace(html, "");
        html = TrailingTagSpaceRegex().Replace(html, ">");
        html = EmptyParagraphRegex().Replace(html, "");
        html = CharWrapParagraphRegex().Replace(html, "$1");
        return html.Trim();
    }

    private static bool IsRemovedElement(string tag, string attributes) => tag switch
    {
        "script" or "style" => true,
        "div" => HasClass(attributes, "abName"),
        "p" => HasClass(attributes, "abLegend"),
        _ => false,
    };

    private static bool HasClass(string attributes, string className)
    {
        var match = ClassAttributeRegex().Match(attributes);
        return match.Success
            && match.Groups["value"].Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Contains(className);
    }

    // Removes every script/style element and every div.abName / p.abLegend together with its content, counting nested
    // elements of the same tag so the matching closing tag ends the removal.
    private static string RemoveElements(string html)
    {
        var result = new System.Text.StringBuilder(html.Length);
        var position = 0;
        foreach (Match tag in TagRegex().Matches(html))
        {
            if (tag.Index < position || tag.Groups["closing"].Value == "/")
            {
                continue;
            }

            var name = tag.Groups["name"].Value.ToLowerInvariant();
            if (!IsRemovedElement(name, tag.Groups["attrs"].Value) || tag.Groups["selfclosing"].Value == "/")
            {
                continue;
            }

            result.Append(html, position, tag.Index - position);
            position = FindElementEnd(html, name, tag.Index + tag.Length);
        }

        result.Append(html, position, html.Length - position);
        return result.ToString();
    }

    private static int FindElementEnd(string html, string name, int contentStart)
    {
        var depth = 1;
        foreach (Match tag in TagRegex().Matches(html, contentStart))
        {
            if (!string.Equals(tag.Groups["name"].Value, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            depth += tag.Groups["closing"].Value == "/" ? -1 : 1;
            if (depth == 0)
            {
                return tag.Index + tag.Length;
            }
        }

        return html.Length;
    }

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"<(?<closing>/?)(?<name>[a-zA-Z][a-zA-Z0-9]*)(?<attrs>[^>]*?)(?<selfclosing>/?)>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\sclass\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)')")]
    private static partial Regex ClassAttributeRegex();

    // <a ...> / </a> and <i> / </i>, but not <img>, <input> or similar.
    [GeneratedRegex(@"</?(?:a|i)(?=[\s>/])[^>]*>")]
    private static partial Regex UnwrapTagsRegex();

    // The attribute is only removed inside a tag, which the leading whitespace + trailing lookahead approximates well
    // enough for this source: none of the listed names occurs in cell text followed by "=".
    [GeneratedRegex(@"(?<=<[a-zA-Z][^>]*)\s(?:style|width|height|cellspacing|cellpadding|border)(?=[\s=>/])(?:\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]+))?[""']*")]
    private static partial Regex RemovedAttributeRegex();

    // A removed attribute can leave a space before the closing ">".
    [GeneratedRegex(@"(?<=<[a-zA-Z][^>]*?)\s+>")]
    private static partial Regex TrailingTagSpaceRegex();

    [GeneratedRegex(@"<p[^>]*>(?:\s|&nbsp;|<br\s*/?>)*</p>", RegexOptions.IgnoreCase)]
    private static partial Regex EmptyParagraphRegex();

    [GeneratedRegex(@"<p[^>]*>(\s*<div[^>]*class=""dsCharWrap""[^>]*>[\s\S]*?</div>[\s\S]*?)</p>", RegexOptions.IgnoreCase)]
    private static partial Regex CharWrapParagraphRegex();
}
