using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Servitor.DataUpdate.CoreRules;

/// <summary>
/// Reduces the HTML of one core-rules section to plain structure: paragraphs, lists, tables and bold / italic /
/// underline text. Tooltip spans, links, layout divs and all attributes are dropped, loose text is wrapped in
/// paragraphs. The result is what <c>core/*.json</c> holds and what <c>HtmlToMarkdown</c> reads later.
/// </summary>
public static partial class CoreRuleHtml
{
    private static readonly HashSet<string> VoidTags = new(StringComparer.OrdinalIgnoreCase) { "br", "hr", "img", "input", "meta", "link", "wbr" };
    private static readonly HashSet<string> DroppedTags = new(StringComparer.OrdinalIgnoreCase) { "script", "style", "hr", "img", "input", "iframe", "svg" };
    private static readonly HashSet<string> InlineTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "b", "strong", "i", "em", "u", "sub", "sup", "span", "a", "font", "small", "mark", "abbr", "br", "code",
    };

    public static string Normalize(string html)
    {
        var root = Parse(CommentRegex().Replace(html, ""));
        var output = new StringBuilder();
        WriteBlocks(root.Children, output);
        return output.ToString();
    }

    /// <summary>The visible text of an HTML fragment with entities decoded and whitespace collapsed.</summary>
    public static string PlainText(string html) =>
        WhitespaceRegex().Replace(WebUtility.HtmlDecode(TagRegex().Replace(html, "")), " ").Trim();

    // ---- tree --------------------------------------------------------------------------------------------------

    private sealed class Node(string name, string? text = null, string attributes = "")
    {
        public string Name { get; } = name;

        public string Attributes { get; } = attributes;

        public string? Text { get; } = text;

        public List<Node> Children { get; } = [];

        public bool IsText => Text is not null;
    }

    private static Node Parse(string html)
    {
        var root = new Node("#root");
        var stack = new List<Node> { root };
        var position = 0;

        foreach (Match tag in TagRegex().Matches(html))
        {
            AddText(stack[^1], html[position..tag.Index]);
            position = tag.Index + tag.Length;

            var name = tag.Groups["name"].Value.ToLowerInvariant();
            if (tag.Groups["closing"].Value == "/")
            {
                var index = stack.FindLastIndex(n => n.Name == name);
                if (index > 0)
                {
                    stack.RemoveRange(index, stack.Count - index);
                }

                continue;
            }

            CloseImpliedElements(stack, name);
            var node = new Node(name, attributes: tag.Groups["attrs"].Value);
            stack[^1].Children.Add(node);
            if (!VoidTags.Contains(name) && tag.Groups["selfclosing"].Value != "/")
            {
                stack.Add(node);
            }
        }

        AddText(stack[^1], html[position..]);
        return root;
    }

    private static void AddText(Node parent, string text)
    {
        if (text.Length > 0)
        {
            parent.Children.Add(new Node("#text", text));
        }
    }

    // A new <li> ends the open <li>, a new <td> the open <td>, and so on, but never across a list or table boundary.
    private static void CloseImpliedElements(List<Node> stack, string opening)
    {
        string[] closes = opening switch
        {
            "li" => ["li"],
            "td" or "th" => ["td", "th"],
            "tr" => ["td", "th", "tr"],
            "p" => ["p"],
            _ => [],
        };
        if (closes.Length == 0)
        {
            return;
        }

        for (var i = stack.Count - 1; i > 0; i--)
        {
            var name = stack[i].Name;
            if (closes.Contains(name))
            {
                stack.RemoveRange(i, stack.Count - i);
                return;
            }

            if (name is "ul" or "ol" or "table")
            {
                return;
            }
        }
    }

    // ---- block output ------------------------------------------------------------------------------------------

    private static void WriteBlocks(List<Node> nodes, StringBuilder output)
    {
        var paragraph = new StringBuilder();

        void Flush()
        {
            var content = TrimBreaks(paragraph.ToString());
            paragraph.Clear();
            if (content.Length > 0)
            {
                output.Append("<p>").Append(content).Append("</p>");
            }
        }

        foreach (var node in nodes)
        {
            if (node.IsText)
            {
                paragraph.Append(CleanText(node.Text!));
            }
            else if (IsDropped(node))
            {
                continue;
            }
            else if (node.Name is "ul" or "ol")
            {
                Flush();
                WriteList(node, output);
            }
            else if (node.Name == "table")
            {
                Flush();
                WriteTable(node, output);
            }
            else if (node.Name == "h4")
            {
                Flush();
                output.Append("<h4>").Append(PlainText(string.Concat(node.Children.Select(c => c.Text ?? "")))).Append("</h4>");
            }
            else if (InlineTags.Contains(node.Name))
            {
                paragraph.Append(WriteInline(node, insideBold: false, insideItalic: false, insideUnderline: false));
            }
            else
            {
                Flush();
                WriteBlocks(node.Children, output);
            }
        }

        Flush();
    }

    // Scripts, ad blocks, the FAQ question-and-answer boxes and the "Show" toggle of the FAQ / errata widgets.
    private static bool IsDropped(Node node) =>
        DroppedTags.Contains(node.Name) || IsAnchorMarker(node) || DroppedClassRegex().IsMatch(node.Attributes);

    // <a name="..."></a> is only a jump target.
    private static bool IsAnchorMarker(Node node) => node.Name == "a" && node.Children.Count == 0;

    private static void WriteList(Node list, StringBuilder output)
    {
        output.Append("<ul>");
        foreach (var item in list.Children.Where(c => c.Name == "li"))
        {
            output.Append("<li>");
            WriteBlocks(item.Children, output);
            output.Append("</li>");
        }

        output.Append("</ul>");
    }

    private static void WriteTable(Node table, StringBuilder output)
    {
        // The source wraps some tables in another one-cell table for layout; only the inner one is written.
        if (SoleNestedTable(table) is { } inner)
        {
            WriteTable(inner, output);
            return;
        }

        output.Append("<table>");
        foreach (var row in Descendants(table, "tr"))
        {
            output.Append("<tr>");
            foreach (var cell in row.Children.Where(c => c.Name is "td" or "th"))
            {
                output.Append("<td>");
                WriteBlocks(cell.Children, output);
                output.Append("</td>");
            }

            output.Append("</tr>");
        }

        output.Append("</table>");
    }

    private static Node? SoleNestedTable(Node table)
    {
        var rows = Descendants(table, "tr").ToList();
        if (rows.Count != 1)
        {
            return null;
        }

        var cells = rows[0].Children.Where(c => c.Name is "td" or "th").ToList();
        if (cells.Count != 1)
        {
            return null;
        }

        var content = cells[0].Children.Where(c => !(c.IsText && string.IsNullOrWhiteSpace(c.Text))).ToList();
        return content.Count == 1 && content[0].Name == "table" ? content[0] : null;
    }

    private static IEnumerable<Node> Descendants(Node node, string name)
    {
        foreach (var child in node.Children)
        {
            if (child.Name == name)
            {
                yield return child;
            }
            else if (child.Name is "thead" or "tbody" or "tfoot")
            {
                foreach (var nested in Descendants(child, name))
                {
                    yield return nested;
                }
            }
        }
    }

    // ---- inline output -----------------------------------------------------------------------------------------

    private static string WriteInline(Node node, bool insideBold, bool insideItalic, bool insideUnderline)
    {
        if (node.IsText)
        {
            return CleanText(node.Text!);
        }

        if (IsDropped(node))
        {
            return "";
        }

        if (node.Name == "br")
        {
            return "<br>";
        }

        var isKeyword = node.Name == "span" && KeywordClassRegex().IsMatch(node.Attributes);
        var bold = insideBold || isKeyword || node.Name is "b" or "strong";
        var italic = insideItalic || node.Name is "i" or "em";
        var underline = insideUnderline;
        var inner = string.Concat(node.Children.Select(c => WriteInline(c, bold, italic, underline)));

        // A style is only written where it starts, so nested <b><b>x</b></b> becomes <b>x</b>.
        var tag = node.Name switch
        {
            "b" or "strong" when !insideBold => "b",
            "span" when isKeyword && !insideBold => "b",
            "i" or "em" when !insideItalic => "i",
            "sub" or "sup" => node.Name,
            _ => null,
        };
        return tag is null || inner.Trim().Length == 0 ? inner : $"<{tag}>{inner}</{tag}>";
    }

    private static string CleanText(string text) => WhitespaceRegex().Replace(text, " ");

    private static string TrimBreaks(string paragraph)
    {
        var trimmed = paragraph.Trim();
        while (true)
        {
            var next = LeadingBreakRegex().Replace(TrailingBreakRegex().Replace(trimmed, ""), "").Trim();
            if (next == trimmed)
            {
                return next.Replace("&nbsp;", " ").Trim().Length == 0 ? "" : next;
            }

            trimmed = next;
        }
    }

    [GeneratedRegex(@"<(?<closing>/?)(?<name>[a-zA-Z][a-zA-Z0-9]*)(?<attrs>[^>]*?)(?<selfclosing>/?)>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"class\s*=\s*[""'][^""']*(?<![a-zA-Z_-])(?:faq|btnFaqErrataToggle|page_breaker_ads|noprint)(?![a-zA-Z_-])")]
    private static partial Regex DroppedClassRegex();

    // Wahapedia marks keywords such as [LETHAL HITS] or CHARACTER with <span class="kwb">.
    [GeneratedRegex(@"class\s*=\s*[""'][^""']*(?<![a-zA-Z])kwb(?![a-zA-Z])")]
    private static partial Regex KeywordClassRegex();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^(?:\s|<br>)+")]
    private static partial Regex LeadingBreakRegex();

    [GeneratedRegex(@"(?:\s|<br>)+$")]
    private static partial Regex TrailingBreakRegex();
}
