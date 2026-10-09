using Servitor.RulesData.Html;

namespace Servitor.App.Tests.Shared.Html;

public class HtmlToMarkdownTests
{
    [Fact]
    public void Converts_bold_and_keyword_spans_to_double_asterisks()
    {
        var result = HtmlToMarkdown.Convert("Target the <span class=\"kwb\">INFANTRY</span> unit and <b>battle-shock</b> it.");

        Assert.Equal("Target the **INFANTRY** unit and **battle-shock** it.", result);
    }

    [Fact]
    public void Nested_bold_tags_collapse_into_one_marker_pair()
    {
        Assert.Equal("**bodyguard** unit", HtmlToMarkdown.Convert("<b><b>bodyguard</b></b> unit"));
    }

    [Fact]
    public void Whitespace_at_the_edge_of_a_bold_run_moves_outside_the_markers()
    {
        Assert.Equal("**Feel No Pain** 4+", HtmlToMarkdown.Convert("<b>Feel No Pain </b>4+"));
    }

    [Fact]
    public void Adjacent_bold_runs_stay_separate_words()
    {
        Assert.Equal("**SOURCE** **OF**", HtmlToMarkdown.Convert("<b>SOURCE</b><b>OF</b>"));
    }

    [Fact]
    public void Line_breaks_become_newlines_and_double_breaks_a_blank_line()
    {
        Assert.Equal(
            "**WHEN:** Now.\n\n**TARGET:** One unit.",
            HtmlToMarkdown.Convert("<b>WHEN:</b> Now.<br><br><b>TARGET:</b> One unit."));
    }

    [Fact]
    public void Lists_become_dash_items_and_nested_lists_indent()
    {
        var result = HtmlToMarkdown.Convert("<ul><li>One</li><li>Two<ul><li>Inner</li></ul></li></ul>");

        Assert.Equal("- One\n- Two\n  - Inner", result);
    }

    [Fact]
    public void Ordered_lists_are_numbered()
    {
        Assert.Equal("1. First\n2. Second", HtmlToMarkdown.Convert("<ol><li>First</li><li>Second</li></ol>"));
    }

    [Fact]
    public void Tables_become_pipe_tables_with_a_header_separator()
    {
        var result = HtmlToMarkdown.Convert("<table><tr><td>A</td><td>B</td></tr><tr><td>1</td><td>2</td></tr></table>");

        Assert.Equal("| A | B |\n| --- | --- |\n| 1 | 2 |", result);
    }

    [Fact]
    public void Underline_and_unknown_tags_are_dropped_but_their_text_is_kept()
    {
        Assert.Equal("is not eligible", HtmlToMarkdown.Convert("is <u>not</u> <marquee>eligible</marquee>"));
    }

    [Fact]
    public void Html_entities_are_decoded()
    {
        Assert.Equal("Rock & Roll", HtmlToMarkdown.Convert("Rock &amp; Roll"));
    }

    [Fact]
    public void Empty_input_yields_an_empty_string()
    {
        Assert.Equal(string.Empty, HtmlToMarkdown.Convert("  "));
    }
}
