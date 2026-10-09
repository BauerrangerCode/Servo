using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Servitor.DataUpdate.Wahapedia;

namespace Servitor.DataUpdate.CoreRules;

/// <summary>One rule section: <paramref name="Number"/> is the printed rule number ("11.02") or null for an unnumbered heading.</summary>
public sealed record CoreRuleEntry(string Id, string? Number, string Name, string Description);

/// <summary>One <c>core/&lt;name&gt;.json</c>: the sections of one chapter of the core rules.</summary>
public sealed record CoreRuleFile(string FileName, IReadOnlyList<CoreRuleEntry> Entries)
{
    public string ToJson() => new JsonArray(Entries
            .Select(e => (JsonNode?)new JsonObject
            {
                ["id"] = e.Id,
                ["number"] = e.Number,
                ["name"] = e.Name,
                ["description"] = e.Description,
            })
            .ToArray())
        .ToJsonString(WriteOptions);

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}

/// <summary>
/// Splits the Wahapedia core-rules page (https://wahapedia.ru/wh40k11ed/the-rules/core-rules/) into one file per
/// chapter. A chapter starts at an "outline" heading that ends in a two-digit chapter number ("Charge Phase 11");
/// every following content heading (h2/h3 with the page's flex style) becomes a section of it, numbered ones with
/// their rule number as id ("11.02"), unnumbered ones as "&lt;chapter&gt;.&lt;slug&gt;". FAQ and errata headings
/// stay inside the section they belong to, as small headings.
/// </summary>
public static partial class CoreRulesScraper
{
    private const string IntroductionFileName = "introduction";
    private const string IntroductionChapterNumber = "00";
    private const string ContentHeadingStyle = "display:flex";

    private static readonly HashSet<string> ExcludedHeadings = new(StringComparer.OrdinalIgnoreCase) { "See Also" };

    public static IReadOnlyList<CoreRuleFile> Scrape(string pageHtml)
    {
        var files = new List<(string FileName, List<CoreRuleEntry> Entries)>();
        var usedIds = new Dictionary<string, int>();
        var chapterNumber = IntroductionChapterNumber;
        List<CoreRuleEntry>? current = null;

        foreach (var section in ReadSections(pageHtml))
        {
            var (name, number) = SplitNumber(section.HeadingText);
            var isChapter = section.IsOutline && number is not null && ChapterNumberRegex().IsMatch(number);

            string id;
            if (isChapter)
            {
                chapterNumber = number!;
                id = $"{chapterNumber}.{Slugs.Slugify(name)}";
                number = null;
                current = [];
                files.Add((Slugs.Slugify(name), current));
            }
            else
            {
                id = number ?? $"{chapterNumber}.{Slugs.Slugify(name)}";
                if (current is null)
                {
                    current = [];
                    files.Add((IntroductionFileName, current));
                }
            }

            current.Add(new CoreRuleEntry(UniqueId(usedIds, id), number, name, CoreRuleHtml.Normalize(section.Html)));
        }

        return files.Select(f => new CoreRuleFile(f.FileName + ".json", f.Entries)).ToList();
    }

    private sealed record Section(string HeadingText, bool IsOutline, string Html);

    /// <summary>
    /// The sections in page order. A section runs from its content heading to the next content heading; unstyled
    /// headings inside it (FAQ, Errata, See Also) stay as small headings in its text. The page's own UI headings
    /// (h1, and the ad block's styled h4) end the current section without starting a new one.
    /// </summary>
    private static List<Section> ReadSections(string pageHtml)
    {
        var sections = new List<Section>();
        var headings = HeadingRegex().Matches(pageHtml);
        string? headingText = null;
        var isOutline = false;
        var html = new StringBuilder();

        void Close()
        {
            if (headingText is not null)
            {
                sections.Add(new Section(headingText, isOutline, html.ToString()));
            }

            headingText = null;
            html.Clear();
        }

        for (var i = 0; i < headings.Count; i++)
        {
            var heading = headings[i];
            var text = CoreRuleHtml.PlainText(heading.Groups["inner"].Value);
            var segmentStart = heading.Index + heading.Length;
            var segmentEnd = i + 1 < headings.Count ? headings[i + 1].Index : pageHtml.Length;
            var segment = pageHtml[segmentStart..segmentEnd];

            if (IsContentHeading(heading) && !ExcludedHeadings.Contains(text))
            {
                Close();
                headingText = text;
                isOutline = heading.Groups["attrs"].Value.Contains("outline_header", StringComparison.Ordinal);
                html.Append(segment);
            }
            else if (IsPageChrome(heading))
            {
                Close();
            }
            else if (headingText is not null)
            {
                html.Append("<h4>").Append(WebUtility.HtmlEncode(text)).Append("</h4>").Append(segment);
            }
        }

        Close();
        return sections;
    }

    private static bool IsPageChrome(Match heading) =>
        heading.Groups["level"].Value == "1"
        || (heading.Groups["level"].Value == "4" && heading.Groups["attrs"].Value.Contains("style=", StringComparison.Ordinal));

    private static bool IsContentHeading(Match heading) =>
        heading.Groups["level"].Value is "2" or "3"
        && heading.Groups["attrs"].Value.Replace(" ", "").Contains(ContentHeadingStyle, StringComparison.Ordinal);

    // The second heading with the same id (the source repeats some numbers) gets "-2", the third "-3", ...
    private static string UniqueId(Dictionary<string, int> usedIds, string id)
    {
        var count = usedIds.GetValueOrDefault(id) + 1;
        usedIds[id] = count;
        return count == 1 ? id : $"{id}-{count}";
    }

    /// <summary>"Armies 01.01" or "01.01 Armies" → ("Armies", "01.01"); "Books" → ("Books", null).</summary>
    private static (string Name, string? Number) SplitNumber(string text)
    {
        var leading = LeadingNumberRegex().Match(text);
        if (leading.Success)
        {
            return (leading.Groups["name"].Value.Trim(), leading.Groups["number"].Value);
        }

        var trailing = TrailingNumberRegex().Match(text);
        return trailing.Success ? (text[..trailing.Index].Trim(), trailing.Groups["number"].Value) : (text, null);
    }

    [GeneratedRegex(@"<h(?<level>[1-4])(?<attrs>[^>]*)>(?<inner>.*?)</h\k<level>>", RegexOptions.Singleline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^(?<number>\d{2}(?:\.\d{2})+)\s+(?<name>.+)$")]
    private static partial Regex LeadingNumberRegex();

    [GeneratedRegex(@"\s(?<number>\d{2}(?:\.\d{2})*)$")]
    private static partial Regex TrailingNumberRegex();

    [GeneratedRegex(@"^\d{2}$")]
    private static partial Regex ChapterNumberRegex();
}
