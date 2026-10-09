using Servitor.RulesData.Domain;
using Servitor.RulesData.Mapping;

namespace Servitor.App.Tests.RulesData.Mapping;

public class ChapterRestrictionParserTests
{
    [Fact]
    public void The_restriction_sentence_yields_the_chapter_name_as_written()
    {
        var html = "<p>Bonus.</p><p>RESTRICTIONS Your army can include <b>Iron Hands</b> units, but it cannot include any ADEPTUS ASTARTES units drawn from any other Chapter.</p>";

        Assert.Equal("Iron Hands", ChapterRestrictionParser.Find([html]));
    }

    [Fact]
    public void Markup_between_the_words_line_breaks_and_case_do_not_matter()
    {
        var html = "Your army can include<br>SPACE   WOLVES units,\nbut it cannot include any <i>Adeptus Astartes</i> units drawn from any other chapter.";

        Assert.Equal("SPACE   WOLVES".Replace("   ", " "), ChapterRestrictionParser.Find([html]));
    }

    [Fact]
    public void The_first_description_that_states_it_wins_and_others_are_ignored()
    {
        Assert.Equal("Blood Angels", ChapterRestrictionParser.Find(["No rule here.", "Your army can include Blood Angels units, but it cannot include any ADEPTUS ASTARTES units drawn from any other Chapter."]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Your army can include Astra Militarum units, even though they do not have the Faction keyword.")]
    [InlineData("You cannot include any ADEPTUS ASTARTES units drawn from any other Chapter.")]
    public void Other_wording_is_not_a_chapter_restriction(string html)
    {
        Assert.Null(ChapterRestrictionParser.Find([html]));
    }

    [Fact]
    public void Real_space_marines_detachments_carry_their_chapter_and_others_do_not()
    {
        var faction = RulesTextMapper.MapFaction(File.ReadAllText(TestPaths.WwwrootDataFile("factions", "space-marines", "faction.json")));

        Assert.Equal("Black Templars", faction.Detachments.Single(d => d.Name == "Companions of Vehemence").RestrictedToChapter);
        Assert.Equal("Iron Hands", faction.Detachments.Single(d => d.Name == "Hammer of Avernii").RestrictedToChapter);
        Assert.Null(faction.Detachments.Single(d => d.Name == "Gladius Task Force").RestrictedToChapter);
        Assert.InRange(faction.Detachments.Count(d => d.RestrictedToChapter is not null), 20, 40);
    }
}
