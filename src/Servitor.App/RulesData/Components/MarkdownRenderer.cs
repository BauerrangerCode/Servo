using Markdig;
using Microsoft.AspNetCore.Components;

namespace Servitor.App.RulesData.Components;

/// <summary>
/// Renders rules-text Markdown (see <c>Servitor.RulesData.Html.HtmlToMarkdown</c>) as safe HTML. Raw HTML in the
/// source is escaped rather than passed through, since the result is rendered as markup. Not thread-safe, which the
/// single-threaded browser does not need.
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().DisableHtml().Build();

    private const int MaxCachedTexts = 4000;

    // Converting is slow in the browser and a screen re-renders its rule texts on every change, so each text is converted once.
    private static readonly Dictionary<string, string> Cache = [];

    public static MarkupString Render(string markdown)
    {
        if (!Cache.TryGetValue(markdown, out var html))
        {
            if (Cache.Count >= MaxCachedTexts)
            {
                Cache.Clear();
            }

            Cache[markdown] = html = Markdown.ToHtml(markdown, Pipeline);
        }

        return new MarkupString(html);
    }
}
