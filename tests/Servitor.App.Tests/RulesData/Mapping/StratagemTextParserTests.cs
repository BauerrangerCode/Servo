using Servitor.RulesData.Mapping;

namespace Servitor.App.Tests.RulesData.Mapping;

public class StratagemTextParserTests
{
    [Fact]
    public void Splits_the_four_standard_blocks_into_their_fields()
    {
        var result = StratagemTextParser.Parse(
            "**WHEN:** Your Fight phase.\n\n**TARGET:** One unit.\n\n**EFFECT:** It fights.\n\n**RESTRICTIONS:** Once per turn.");

        Assert.Equal("Your Fight phase.", result.When);
        Assert.Equal("One unit.", result.Target);
        Assert.Equal("It fights.", result.Effect);
        Assert.Equal("Once per turn.", result.Restrictions);
        Assert.Equal(string.Empty, result.AdditionalText);
    }

    [Fact]
    public void Blocks_with_other_labels_are_kept_in_additional_text()
    {
        var result = StratagemTextParser.Parse("**ELIGIBLE IF:** Unengaged.\n\n**EFFECT:** It moves.");

        Assert.Equal("It moves.", result.Effect);
        Assert.Equal("**ELIGIBLE IF:** Unengaged.", result.AdditionalText);
    }

    [Fact]
    public void A_list_after_a_label_stays_on_its_own_lines()
    {
        var result = StratagemTextParser.Parse("**AFTER MOVING:**\n- Must be unengaged.\n- No charge.");

        Assert.Equal("**AFTER MOVING:**\n- Must be unengaged.\n- No charge.", result.AdditionalText);
    }

    [Fact]
    public void Text_before_the_first_label_is_not_lost()
    {
        var result = StratagemTextParser.Parse("Introductory note.\n\n**WHEN:** Now.");

        Assert.Equal("Now.", result.When);
        Assert.Equal("Introductory note.", result.AdditionalText);
    }

    [Fact]
    public void A_description_without_labels_lands_entirely_in_additional_text()
    {
        var result = StratagemTextParser.Parse("Just prose.");

        Assert.Equal(string.Empty, result.Effect);
        Assert.Equal("Just prose.", result.AdditionalText);
    }
}
