using Servitor.DataUpdate.CoreRules;

namespace Servitor.App.Tests.DataUpdate;

public class CoreRuleHtmlTests
{
    [Fact]
    public void Loose_text_is_wrapped_in_a_paragraph_and_layout_divs_are_flattened()
    {
        Assert.Equal("<p>Text</p><p>Framed</p>", CoreRuleHtml.Normalize("Text<div><br></div><div class=\"frame\">Framed</div>"));
    }

    [Fact]
    public void Tooltips_and_links_are_reduced_to_their_text_and_keywords_become_bold()
    {
        var html = "A <b><b><span class=\"tooltip1\"><span class=\"tt kwbu\">unit</span></span></b></b> with <a href=\"/x\">a link</a> and <span class=\"kwb\">[BLAST]</span>.";

        Assert.Equal("<p>A <b>unit</b> with a link and <b>[BLAST]</b>.</p>", CoreRuleHtml.Normalize(html));
    }

    [Fact]
    public void Ordered_and_unordered_lists_become_lists_of_paragraphs()
    {
        Assert.Equal(
            "<ul><li><p>One</p></li><li><p>Two</p><ul><li><p>Nested</p></li></ul></li></ul>",
            CoreRuleHtml.Normalize("<ol><li>One</li><li>Two<ul><li>Nested</li></ul></li></ol>"));
    }

    [Fact]
    public void A_list_item_that_is_not_closed_ends_at_the_next_one()
    {
        Assert.Equal("<ul><li><p>One</p></li><li><p>Two</p></li></ul>", CoreRuleHtml.Normalize("<ul><li>One<li>Two</ul>"));
    }

    [Fact]
    public void Tables_keep_rows_and_cells_and_a_layout_table_around_a_table_is_unwrapped()
    {
        var html = "<table><tbody><tr><td><table><tr><td>A</td><td>B</td></tr></table></td></tr></tbody></table>";

        Assert.Equal("<table><tr><td><p>A</p></td><td><p>B</p></td></tr></table>", CoreRuleHtml.Normalize(html));
    }

    [Fact]
    public void Italic_text_is_kept_and_underline_is_dropped()
    {
        Assert.Equal("<p><i>Note</i> that x</p>", CoreRuleHtml.Normalize("<i>Note</i> that <u>x</u>"));
    }

    [Fact]
    public void Scripts_ads_comments_anchors_and_empty_paragraphs_are_removed()
    {
        var html = "<a name=\"x\"></a><script>ads()</script><div class=\"noprint page_breaker_ads\">ad</div><!-- c --><p> <br> </p>Real";

        Assert.Equal("<p>Real</p>", CoreRuleHtml.Normalize(html));
    }

    [Fact]
    public void Line_breaks_inside_a_paragraph_stay_but_leading_and_trailing_ones_go()
    {
        Assert.Equal("<p>a<br>b</p>", CoreRuleHtml.Normalize("<br>a<br>b<br><br>"));
    }

    [Fact]
    public void Stray_closing_tags_are_ignored()
    {
        Assert.Equal("<p>Text</p>", CoreRuleHtml.Normalize("Text</div></span></b>"));
    }

    [Fact]
    public void Plain_text_decodes_entities_and_collapses_whitespace()
    {
        Assert.Equal("Fast & loose", CoreRuleHtml.PlainText("<b>Fast</b>  &amp;\n loose"));
    }
}
