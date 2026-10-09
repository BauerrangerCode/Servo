using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.ArmyBuilder.State;
using Servitor.App.ArmyImport.Pages;
using Servitor.App.RulesData.Loading;
using Servitor.App.Tests.ArmyImport.Resolution;
using Servitor.App.Tests.RulesData.Loading;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.ArmyImport.Pages;

public class ImportTests : BunitContext
{
    private sealed class FakeRulesDataProvider(MergedRulesData? data) : IMergedRulesDataProvider
    {
        public event Action? DataChanged
        {
            add { }
            remove { }
        }

        public Task<MergedRulesData?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(data);

        public Task StoreAsync(MergedRulesData value, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public ImportTests()
    {
        Services.AddSingleton<IArmyBuilderState>(new ArmyBuilderState());
        Services.AddSingleton<IMergedRulesDataProvider>(new FakeRulesDataProvider(null));
        Services.AddSingleton<IDatasheetCatalog>(new FakeDatasheetCatalog());
    }

    [Fact]
    public void Shows_a_paste_textarea_and_a_disabled_import_action_when_empty()
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
    public void Pasting_text_updates_the_bound_value_and_enables_the_button()
    {
        var cut = Render<Import>();

        cut.Find(".import-textarea").Input("some pasted army list");

        Assert.Equal("some pasted army list", cut.Find(".import-textarea").GetAttribute("value"));
        Assert.False(cut.Find(".import-play").HasAttribute("disabled"));
    }

    [Fact]
    public void An_unparseable_paste_shows_the_parser_errors_and_does_not_navigate()
    {
        var cut = Render<Import>();
        var nav = Services.GetRequiredService<NavigationManager>();

        cut.Find(".import-textarea").Input("this is not a Warscribe export");
        cut.Find(".import-play").Click();

        Assert.Contains("doesn't look like a Warscribe 40K", cut.Find(".import-errors").TextContent);
        Assert.EndsWith("/", nav.Uri);
    }

    [Fact]
    public void A_successful_import_populates_state_and_navigates_to_play()
    {
        var rulesData = ArmyExportResolverTests.LoadRealMergedData();
        Services.AddSingleton<IMergedRulesDataProvider>(new FakeRulesDataProvider(rulesData));
        Services.AddSingleton<IDatasheetCatalog>(new FakeDatasheetCatalog(rulesData.Datasheets));
        var state = new ArmyBuilderState();
        Services.AddSingleton<IArmyBuilderState>(state);

        var cut = Render<Import>();
        var nav = Services.GetRequiredService<NavigationManager>();

        cut.Find(".import-textarea").Input(File.ReadAllText(TestPaths.DataFile("Army1.txt")));
        cut.Find(".import-play").Click();

        Assert.Equal("orks", state.Setup.FactionId);
        Assert.Equal(16, state.Units.Count);
        Assert.EndsWith("/play", nav.Uri);
    }
}
