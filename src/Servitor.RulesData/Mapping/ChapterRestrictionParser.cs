using System.Net;
using System.Text.RegularExpressions;

namespace Servitor.RulesData.Mapping;

/// <summary>
/// Finds the Chapter restriction a Space Marines detachment states in its rules text: "Your army can include Iron
/// Hands units, but it cannot include any ADEPTUS ASTARTES units drawn from any other Chapter." The source has no
/// structured field for it, so the sentence is read; any other wording is not recognised.
/// </summary>
public static partial class ChapterRestrictionParser
{
    /// <returns>The chapter name as written ("Iron Hands", "SPACE WOLVES"), or null when no description states the restriction.</returns>
    public static string? Find(IEnumerable<string> descriptionsHtml)
    {
        foreach (var html in descriptionsHtml)
        {
            var text = WhitespaceRegex().Replace(WebUtility.HtmlDecode(TagRegex().Replace(html, " ")), " ");
            if (RestrictionRegex().Match(text) is { Success: true } match)
            {
                return match.Groups["chapter"].Value.Trim();
            }
        }

        return null;
    }

    [GeneratedRegex(@"can include (?<chapter>[^.<>]+?) units,? but it cannot include any ADEPTUS ASTARTES units drawn from any other Chapter", RegexOptions.IgnoreCase)]
    private static partial Regex RestrictionRegex();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
