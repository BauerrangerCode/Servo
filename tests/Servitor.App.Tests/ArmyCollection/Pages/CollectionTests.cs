using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.ArmyBuilder.State;
using Servitor.App.ArmyCollection.Pages;
using Servitor.App.ArmyCollection.State;
using Servitor.App.RulesData.Loading;
using Servitor.App.Tests.ArmyCollection;
using Servitor.App.Tests.RulesData.Loading;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.ArmyCollection.Pages;

public class CollectionTests : BunitContext
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

    private static Datasheet Sheet(string name) => new(
        name, "Orks", 45, new StatLine("6\"", "3", "5+", null, "1", "7+", "2"), [], [], [], [], [], [], null, [])
    {
        FactionSlugs = ["orks"],
    };

    private static readonly Detachment GreenTide = new("d1", "Green Tide", "", 1, MissionArchetype.TakeAndHold, 2, GameMode.Standard);

    private static MergedRulesData RulesData() => new(
        "v1", DateTimeOffset.UnixEpoch, [Sheet("Painboy")], [], [new MergedFactionRules("orks", "Orks", [], [], [], [GreenTide])], []);

    private static SavedArmy Sample(string name) => new(
        Guid.NewGuid(), name, DateTimeOffset.UtcNow, "orks", "Strike Force", [], ["Green Tide"],
        [new SavedUnit(Guid.NewGuid(), "Painboy", 0, WargearSelection.Default, null, null, false, null)]);

    private IRenderedComponent<Collection> RenderCollection(IArmyCollectionStore store, ArmyBuilderState? state = null)
    {
        Services.AddSingleton(store);
        Services.AddSingleton<IArmyBuilderState>(state ?? new ArmyBuilderState());
        Services.AddSingleton<IMergedRulesDataProvider>(new FakeRulesDataProvider(RulesData()));
        Services.AddSingleton<IDatasheetCatalog>(new FakeDatasheetCatalog(RulesData().Datasheets));
        return Render<Collection>();
    }

    [Fact]
    public void Shows_a_hint_when_no_armies_are_saved()
    {
        var cut = RenderCollection(new FakeArmyCollectionStore());

        Assert.Contains("No saved armies yet", cut.Markup);
    }

    [Fact]
    public async Task Lists_a_saved_army_with_its_details()
    {
        var store = new FakeArmyCollectionStore();
        await store.SaveAsync(Sample("My Waaagh"));

        var cut = RenderCollection(store);

        var entry = cut.Find(".collection-entry");
        Assert.Contains("My Waaagh", entry.TextContent);
        Assert.Contains("Orks", entry.TextContent);
        Assert.Contains("Strike Force", entry.TextContent);
    }

    [Fact]
    public async Task Opening_a_saved_army_in_build_loads_it_and_navigates()
    {
        var store = new FakeArmyCollectionStore();
        await store.SaveAsync(Sample("My Waaagh"));
        var state = new ArmyBuilderState();

        var cut = RenderCollection(store, state);
        cut.Find(".collection-entry button").Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Equal("orks", state.Setup.FactionId);
        Assert.EndsWith("/build", nav.Uri);
    }

    [Fact]
    public async Task A_stale_reference_shows_errors_and_does_not_navigate()
    {
        var store = new FakeArmyCollectionStore();
        await store.SaveAsync(Sample("My Waaagh") with { DetachmentNames = ["Unknown Detachment"] });

        var cut = RenderCollection(store);
        cut.Find(".collection-entry button").Click();

        Assert.Contains("Unknown detachment", cut.Find(".collection-errors").TextContent);
        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.DoesNotContain("/build", nav.Uri);
    }

    [Fact]
    public async Task Delete_requires_a_second_confirming_click()
    {
        var store = new FakeArmyCollectionStore();
        await store.SaveAsync(Sample("My Waaagh"));

        var cut = RenderCollection(store);
        cut.Find(".delete-button").Click();

        Assert.Empty(cut.FindAll(".delete-button"));
        Assert.Single(store.Armies);
        Assert.Contains("Delete", cut.Find(".confirm-delete").TextContent);
    }

    [Fact]
    public async Task Cancelling_the_confirmation_keeps_the_army()
    {
        var store = new FakeArmyCollectionStore();
        await store.SaveAsync(Sample("My Waaagh"));

        var cut = RenderCollection(store);
        cut.Find(".delete-button").Click();
        cut.Find(".cancel-delete").Click();

        Assert.Single(store.Armies);
        Assert.Single(cut.FindAll(".delete-button"));
    }

    [Fact]
    public async Task Confirming_deletes_the_army_and_refreshes_the_list()
    {
        var store = new FakeArmyCollectionStore();
        await store.SaveAsync(Sample("My Waaagh"));

        var cut = RenderCollection(store);
        cut.Find(".delete-button").Click();
        cut.Find(".confirm-delete-yes").Click();

        Assert.Empty(store.Armies);
        Assert.Contains("No saved armies yet", cut.Markup);
    }
}
