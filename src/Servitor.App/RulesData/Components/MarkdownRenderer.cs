using Markdig;
using Microsoft.AspNetCore.Components;

namespace Servitor.App.RulesData.Components;

/// <summary>
/// Renders rules-text Markdown (see <c>Servitor.RulesData.Html.HtmlToMarkdown</c>) as safe HTML. Raw HTML in the
/// source is escaped rather than passed through, since the result is rendered as markup.
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().DisableHtml().Build();

    public static MarkupString Render(string markdown) => new(Markdown.ToHtml(markdown, Pipeline));
}
