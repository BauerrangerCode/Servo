using Bunit;
using Servitor.App.ArmyImport.Pages;

namespace Servitor.App.Tests.ArmyImport.Pages;

public class ImportTests : BunitContext
{
    [Fact]
    public void Shows_a_paste_textarea_and_a_disabled_import_action()
    {
        var cut = Render<Import>();

        Assert.Single(cut.FindAll(".import-textarea"));
        Assert.True(cut.Find(".import-play").HasAttribute("disabled"));
    }

    [Fact]
    public void Offers_a_fallback_to_build()
    {
        var cut = Render<Import>();

        var fallback = cut.Find(".build-fallback");
        Assert.Equal("build", fallback.GetAttribute("href"));
    }

    [Fact]
    public void Pasting_text_updates_the_bound_value()
    {
        var cut = Render<Import>();

        cut.Find(".import-textarea").Input("some pasted army list");

        Assert.Equal("some pasted army list", cut.Find(".import-textarea").GetAttribute("value"));
    }
}
