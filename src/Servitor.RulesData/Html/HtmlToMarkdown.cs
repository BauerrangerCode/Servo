using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Servitor.RulesData.Html;

/// <summary>
/// Converts the inline HTML of the rules-text source (Wahapedia-style <c>&lt;b&gt;</c>, keyword spans, lists,
/// tables) into the Markdown dialect BSData uses for rule text: <c>**bold**</c>, <c>*italic*</c>, <c>- </c> lists.
/// Tags without a Markdown equivalent (underline, unknown elements) are dropped, their text is kept.
/// </summary>
public static partial class HtmlToMarkdown
{
    private const string KeywordSpanClassMarker = "kwb";
    private const string BoldMarker = "**";
    private const string ItalicMarker = "*";
    private const string ListIndent = "  ";

    public static string Convert(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var writer = new MarkdownWriter();
        var position = 0;
        foreach (Match tag in TagPattern().Matches(html))
        {
            writer.AppendText(html[position..tag.Index]);
            writer.Apply(
                tag.Groups["name"].Value.ToLowerInvariant(),
                isClosing: tag.Groups["closing"].Value == "/",
                attributes: tag.Groups["attrs"].Value);
            position = tag.Index + tag.Length;
        }

        writer.AppendText(html[position..]);
        return writer.Build();
    }

    [GeneratedRegex(@"<(?<closing>/?)(?<name>[a-zA-Z][a-zA-Z0-9]*)(?<attrs>[^>]*)>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex HorizontalWhitespace();

    [GeneratedRegex(@"^(-|\d+\.) ")]
    private static partial Regex ListItemStart();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessBlankLines();

    [GeneratedRegex(@"class\s*=\s*""(?<value>[^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex ClassAttribute();

    private sealed class MarkdownWriter
    {
        private readonly StringBuilder _output = new();
        private readonly Stack<ListState> _lists = new();
        private readonly Stack<bool> _spanIsKeyword = new();
        private readonly List<List<string>> _tableRows = [];
        private Emphasis _bold;
        private Emphasis _italic;
        private int _cellStart = -1;

        public void AppendText(string rawText)
        {
            if (rawText.Length > 0)
            {
                _output.Append(WebUtility.HtmlDecode(rawText));
            }
        }

        public void Apply(string tagName, bool isClosing, string attributes)
        {
            switch (tagName)
            {
                case "b" or "strong":
                    ToggleEmphasis(isClosing, BoldMarker, ref _bold);
                    break;
                case "i" or "em":
                    ToggleEmphasis(isClosing, ItalicMarker, ref _italic);
                    break;
                case "span":
                    ApplySpan(isClosing, attributes);
                    break;
                case "br":
                    _output.Append('\n');
                    break;
                case "p" or "div" or "h4":
                    EndBlock(blankLine: tagName != "div");
                    if (tagName == "h4" && !isClosing)
                    {
                        _output.Append("#### ");
                    }

                    break;
                case "ul" or "ol":
                    ApplyList(tagName, isClosing);
                    break;
                case "li":
                    ApplyListItem(isClosing);
                    break;
                case "table":
                    ApplyTable(isClosing);
                    break;
                case "tr":
                    ApplyTableRow(isClosing);
                    break;
                case "td" or "th":
                    ApplyTableCell(isClosing);
                    break;
            }
        }

        public string Build()
        {
            var lines = _output.ToString().Split('\n').Select(NormalizeLine);
            return ExcessBlankLines().Replace(string.Join('\n', lines), "\n\n").Trim();
        }

        // Runs of spaces collapse to one, except the indentation that marks a nested list item.
        private static string NormalizeLine(string line)
        {
            var content = HorizontalWhitespace().Replace(line.Trim(), " ");
            var isListItem = ListItemStart().IsMatch(content);
            var indentation = isListItem ? line.Length - line.TrimStart().Length : 0;
            return new string(' ', indentation) + content;
        }

        // Nested or repeated emphasis tags collapse into one marker pair; whitespace at the edges of the span
        // moves outside the markers, because "** text**" is not valid Markdown emphasis.
        private void ToggleEmphasis(bool isClosing, string marker, ref Emphasis emphasis)
        {
            if (!isClosing)
            {
                if (emphasis.Depth++ == 0)
                {
                    // The source glues neighbouring words in separate tags together (<b>SOURCE</b><b>OF</b>);
                    // "**SOURCE****OF**" would be invalid Markdown, so the runs are kept apart by a space.
                    if (emphasis.LastEnd == _output.Length && emphasis.LastEnd > 0)
                    {
                        _output.Append(' ');
                    }

                    emphasis.Start = _output.Length;
                }

                return;
            }

            if (emphasis.Depth == 0 || --emphasis.Depth > 0)
            {
                return;
            }

            var content = _output.ToString(emphasis.Start, _output.Length - emphasis.Start);
            _output.Length = emphasis.Start;
            var trimmed = content.Trim();
            if (trimmed.Length == 0)
            {
                _output.Append(content);
                return;
            }

            var leading = content[..(content.Length - content.TrimStart().Length)];
            var trailing = content[content.TrimEnd().Length..];
            _output.Append(leading).Append(marker).Append(trimmed).Append(marker).Append(trailing);
            emphasis.LastEnd = _output.Length;
        }

        private void ApplySpan(bool isClosing, string attributes)
        {
            if (!isClosing)
            {
                var classValue = ClassAttribute().Match(attributes).Groups["value"].Value;
                var isKeyword = classValue.Contains(KeywordSpanClassMarker, StringComparison.OrdinalIgnoreCase);
                _spanIsKeyword.Push(isKeyword);
                if (isKeyword)
                {
                    ToggleEmphasis(isClosing: false, BoldMarker, ref _bold);
                }

                return;
            }

            if (_spanIsKeyword.Count > 0 && _spanIsKeyword.Pop())
            {
                ToggleEmphasis(isClosing: true, BoldMarker, ref _bold);
            }
        }

        private void EndBlock(bool blankLine)
        {
            if (_output.Length == 0)
            {
                return;
            }

            _output.Append(blankLine ? "\n\n" : "\n");
        }

        private void ApplyList(string tagName, bool isClosing)
        {
            if (isClosing)
            {
                if (_lists.Count > 0)
                {
                    _lists.Pop();
                }

                EndBlock(blankLine: false);
                return;
            }

            EndBlock(blankLine: false);
            _lists.Push(new ListState(tagName == "ol"));
        }

        private void ApplyListItem(bool isClosing)
        {
            if (isClosing || _lists.Count == 0)
            {
                _output.Append('\n');
                return;
            }

            var list = _lists.Peek();
            list.Counter++;
            var marker = list.IsOrdered ? $"{list.Counter}." : "-";
            EnsureLineStart();
            _output.Append(string.Concat(Enumerable.Repeat(ListIndent, _lists.Count - 1))).Append(marker).Append(' ');
        }

        private void EnsureLineStart()
        {
            if (_output.Length > 0 && _output[^1] != '\n')
            {
                _output.Append('\n');
            }
        }

        private void ApplyTable(bool isClosing)
        {
            if (!isClosing)
            {
                EndBlock(blankLine: true);
                _tableRows.Clear();
                return;
            }

            for (var i = 0; i < _tableRows.Count; i++)
            {
                _output.Append("| ").Append(string.Join(" | ", _tableRows[i])).Append(" |\n");
                if (i == 0)
                {
                    _output.Append('|').Append(string.Concat(Enumerable.Repeat(" --- |", _tableRows[0].Count))).Append('\n');
                }
            }

            _tableRows.Clear();
            _output.Append('\n');
        }

        private void ApplyTableRow(bool isClosing)
        {
            if (!isClosing)
            {
                _tableRows.Add([]);
            }
        }

        private void ApplyTableCell(bool isClosing)
        {
            if (!isClosing)
            {
                _cellStart = _output.Length;
                return;
            }

            if (_cellStart < 0 || _tableRows.Count == 0)
            {
                return;
            }

            var cell = _output.ToString(_cellStart, _output.Length - _cellStart);
            _output.Length = _cellStart;
            _cellStart = -1;
            var flattened = HorizontalWhitespace().Replace(cell.Replace("\n", " "), " ").Trim();
            _tableRows[^1].Add(flattened.Replace("|", "\\|"));
        }

        private struct Emphasis
        {
            public int Depth;
            public int Start;
            public int LastEnd;
        }

        private sealed class ListState(bool isOrdered)
        {
            public bool IsOrdered { get; } = isOrdered;

            public int Counter { get; set; }
        }
    }
}
