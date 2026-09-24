using Servitor.DataUpdate.Wahapedia;

namespace Servitor.App.Tests.DataUpdate;

public class WahapediaParsingTests
{
    [Fact]
    public void Csv_headers_become_camel_case_keys_and_the_trailing_column_is_ignored()
    {
        var rows = WahapediaCsv.Parse("﻿datasheet_id|BS_WS|last_update|\r\n000001|3+|x|\r\n");

        var row = Assert.Single(rows);
        Assert.Equal(["datasheetId", "bsWs", "lastUpdate"], row.Keys);
        Assert.Equal("000001", row.Get("datasheetId"));
        Assert.Equal("", row.Get("missing"));
    }

    [Fact]
    public void A_record_may_span_several_lines_and_blank_lines_are_skipped()
    {
        var rows = WahapediaCsv.Parse("id|description|\r\n1|first line\r\nsecond line|\r\n||\r\n2|single|\r\n");

        Assert.Equal(2, rows.Count);
        Assert.Equal("first line\nsecond line", rows[0].Get("description"));
        Assert.Equal("single", rows[1].Get("description"));
    }

    [Fact]
    public void Html_in_cells_is_cleaned()
    {
        var rows = WahapediaCsv.Parse("id|description|\r\n1|<b>Bold</b> <a href=\"/x\">link</a> <i>it</i>|\r\n");

        Assert.Equal("<b>Bold</b> link it", rows[0].Get("description"));
    }

    [Fact]
    public void Name_and_legend_blocks_that_duplicate_other_columns_are_removed_with_their_content()
    {
        var html = "<div class=\"abName\">FIRING <div>DECK</div></div><p class=\"abLegend\">Fluff</p><p>Rules</p><script>x()</script>";

        Assert.Equal("<p>Rules</p>", WahapediaHtml.Strip(html));
    }

    [Fact]
    public void Presentation_attributes_are_removed_but_similarly_named_ones_are_kept()
    {
        var html = "<table border=\"0\" bordercolor=\"#888\" width=\"100%\" class=\"c\"><td style=\"x:y\" height=5>t</td></table>";

        Assert.Equal("<table bordercolor=\"#888\" class=\"c\"><td>t</td></table>", WahapediaHtml.Strip(html));
    }

    [Fact]
    public void A_malformed_style_attribute_and_comments_are_removed()
    {
        Assert.Equal("<div class=\"a\">x</div>", WahapediaHtml.Strip("<div class=\"a\" style=\"\"\"><!-- note -->x</div>"));
    }

    [Fact]
    public void Empty_paragraphs_are_dropped()
    {
        Assert.Equal("<p>a</p>", WahapediaHtml.Strip("<p>a</p><p> <br></p><p>&nbsp;</p>"));
    }

    [Theory]
    [InlineData("Orks", "orks")]
    [InlineData("Emperor's Children", "emperor-s-children")]
    [InlineData("T'au Empire", "t-au-empire")]
    [InlineData("Ünïcode  Nâme!", "unicode-name")]
    [InlineData("!!!", "")]
    public void Slugify_makes_lowercase_ascii_slugs(string input, string expected)
    {
        Assert.Equal(expected, Slugs.Slugify(input));
    }

    [Fact]
    public void Slug_generator_appends_a_counter_to_repeated_slugs_and_uses_the_fallback()
    {
        var next = Slugs.CreateGenerator("thing");

        Assert.Equal("boyz", next("Boyz"));
        Assert.Equal("boyz-2", next("Boyz"));
        Assert.Equal("boyz-3", next("boyz"));
        Assert.Equal("thing", next("???"));
    }
}
