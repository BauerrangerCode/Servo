namespace Servitor.RulesData.Mapping;

/// <summary>
/// Cleans BSData rule text. Besides Markdown, BSData wraps keyword names in <c>^^…^^</c> markers, often interleaved
/// with bold (<c>**^^Infantry^^**</c>, <c>^^**Vehicles^^**</c>). Those markers are not Markdown, and their
/// inconsistent nesting is unusable for rendering, so they are dropped and the bold that accompanies them stays.
/// </summary>
public static class BsDataText
{
    private const string KeywordMarker = "^^";

    public static string CleanMarkdown(string text) => text.Replace(KeywordMarker, "");
}
