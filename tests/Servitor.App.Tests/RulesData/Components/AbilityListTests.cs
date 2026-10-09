using Bunit;
using Servitor.App.RulesData.Components;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.RulesData.Components;

public class AbilityListTests : BunitContext
{
    private IRenderedComponent<AbilityList> Render(params Ability[] abilities) =>
        Render<AbilityList>(p => p
            .Add(c => c.Title, "Abilities")
            .Add(c => c.Abilities, abilities));

    [Fact]
    public void Renders_markdown_bold_and_lists_as_html()
    {
        var cut = Render(new Ability("Aura", "**Feel No Pain** 4+\n\n- one\n- two", AbilityCategory.Named));

        Assert.Equal("Feel No Pain", cut.Find(".ability-description strong").TextContent);
        Assert.Equal(2, cut.FindAll(".ability-description li").Count);
    }

    [Fact]
    public void Raw_html_in_a_description_is_escaped_not_rendered()
    {
        var cut = Render(new Ability("Aura", "<script>alert(1)</script> text", AbilityCategory.Named));

        Assert.Empty(cut.FindAll("script"));
        Assert.Contains("&lt;script&gt;", cut.Find(".ability-description").InnerHtml);
    }

    [Fact]
    public void A_blank_description_renders_no_description_block()
    {
        var cut = Render(new Ability("Aura", " ", AbilityCategory.Named));

        Assert.Empty(cut.FindAll(".ability-description"));
    }
}
