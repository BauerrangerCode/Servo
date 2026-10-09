using Servitor.DataUpdate.CoreRules;

namespace Servitor.App.Tests.DataUpdate;

public class CoreRulesScraperTests
{
    private const string Flex = "style=\"display:flex; align-items:baseline;\"";

    private static string Chapter(string title, string number) =>
        $"<h3 id=\"x\" class=\"outline_header\" {Flex}>{title} {number}</h3>";

    private static string Section(string title, string number, string body) =>
        $"<h2 id=\"y\" {Flex}>{title} <span class=\"h_number\">{number}</span></h2>{body}";

    [Fact]
    public void Each_outline_chapter_becomes_a_file_with_the_chapter_and_its_numbered_sections()
    {
        var page = "<h1>Core Rules</h1>Contents"
            + Chapter("Charge Phase", "11") + "<p class=\"legend\">Intro</p>"
            + Section("1. Start", "11.01", "Start text")
            + Section("2. Charge", "11.02", "Charge text")
            + Chapter("Fight Phase", "12") + Section("1. Start", "12.01", "Fight text");

        var files = CoreRulesScraper.Scrape(page);

        Assert.Equal(["charge-phase.json", "fight-phase.json"], files.Select(f => f.FileName));
        var charge = files[0].Entries;
        Assert.Equal(["11.charge-phase", "11.01", "11.02"], charge.Select(e => e.Id));
        Assert.Equal([null, "11.01", "11.02"], charge.Select(e => e.Number));
        Assert.Equal(["Charge Phase", "1. Start", "2. Charge"], charge.Select(e => e.Name));
        Assert.Equal("<p>Intro</p>", charge[0].Description);
        Assert.Equal("<p>Charge text</p>", charge[2].Description);
    }

    [Fact]
    public void An_unnumbered_heading_takes_the_chapter_number_and_a_slug_as_id()
    {
        var page = Chapter("Terrain", "13") + $"<h3 id=\"z\" {Flex}>Moving Vertically</h3>Climb";

        var entries = CoreRulesScraper.Scrape(page).Single().Entries;

        Assert.Equal("13.moving-vertically", entries[1].Id);
        Assert.Null(entries[1].Number);
    }

    [Fact]
    public void A_number_in_front_of_the_name_is_recognised()
    {
        var page = Chapter("Movement Phase", "09") + $"<h3 id=\"z\" {Flex}>09.05.01 More Than One Move</h3>Text";

        var entry = CoreRulesScraper.Scrape(page).Single().Entries[1];

        Assert.Equal("09.05.01", entry.Id);
        Assert.Equal("More Than One Move", entry.Name);
    }

    [Fact]
    public void Repeated_ids_get_a_counter_suffix()
    {
        var page = Chapter("Movement Phase", "09")
            + Section("Desperate Escape Test", "09.07.01", "A")
            + Section("Desperate Escape", "09.07.01", "B");

        var ids = CoreRulesScraper.Scrape(page).Single().Entries.Select(e => e.Id);

        Assert.Equal(["09.movement-phase", "09.07.01", "09.07.01-2"], ids);
    }

    [Fact]
    public void Headings_before_the_first_chapter_go_into_the_introduction_file()
    {
        var page = $"<h1>Core Rules</h1><h2 id=\"b\" class=\"outline_header_book\" {Flex}>Books</h2>Table" + Chapter("Core Concepts", "01");

        var files = CoreRulesScraper.Scrape(page);

        Assert.Equal(["introduction.json", "core-concepts.json"], files.Select(f => f.FileName));
        Assert.Equal("00.books", files[0].Entries.Single().Id);
    }

    [Fact]
    public void Unstyled_headings_stay_in_the_section_as_small_headings_and_faq_boxes_are_dropped()
    {
        var page = Chapter("Objectives", "14")
            + Section("Control", "14.01", "Rules<h3>Errata</h3><div class=\"btnFaqErrataToggle\">Show</div><div class=\"faq\">Q: A: </div>Corrected text");

        var description = CoreRulesScraper.Scrape(page).Single().Entries[1].Description;

        Assert.Equal("<p>Rules</p><h4>Errata</h4><p>Corrected text</p>", description);
    }

    [Fact]
    public void The_pages_ad_heading_ends_the_current_section_and_the_text_after_it_is_dropped()
    {
        var page = Chapter("Objectives", "14")
            + Section("Control", "14.01", "Rules<h4 style=\"padding-bottom:6px\">Disable Ads</h4>Junk<h3>FAQ</h3>More junk");

        var entries = CoreRulesScraper.Scrape(page).Single().Entries;

        Assert.Equal("<p>Rules</p>", entries[1].Description);
    }

    [Fact]
    public void See_Also_boxes_are_no_sections()
    {
        var page = Chapter("Attack Sequence", "05") + Section("Hit", "05.01", "Text") + $"<h3 id=\"sa\" {Flex}>See Also</h3><ul><li>x</li></ul>";

        var entries = CoreRulesScraper.Scrape(page).Single().Entries;

        Assert.Equal(["05.attack-sequence", "05.01"], entries.Select(e => e.Id));
        Assert.Contains("<h4>See Also</h4><ul><li><p>x</p></li></ul>", entries[1].Description);
    }

    [Fact]
    public void A_page_without_content_headings_yields_no_files()
    {
        Assert.Empty(CoreRulesScraper.Scrape("<html><h1>Nothing</h1><p>text</p></html>"));
    }

    [Fact]
    public void A_file_serializes_to_the_id_number_name_description_shape()
    {
        var file = new CoreRuleFile("x.json", [new CoreRuleEntry("11.01", "11.01", "Start", "<p>It's “fine”</p>"), new CoreRuleEntry("11.a", null, "A", "")]);

        Assert.Equal(
            "[{\"id\":\"11.01\",\"number\":\"11.01\",\"name\":\"Start\",\"description\":\"<p>It's “fine”</p>\"},{\"id\":\"11.a\",\"number\":null,\"name\":\"A\",\"description\":\"\"}]",
            file.ToJson());
    }
}
