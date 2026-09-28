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

    private static Datasheet Sheet(string name, string faction, string factionSlug) => new(
        name, faction, 45, new StatLine("6\"", "3", "5+", null, "1", "7+", "2"), [], [], [], [], [], [], null, [])
    {
        FactionSlugs = [factionSlug],
    };

    private static readonly Detachment GreenTide = new("d1", "Green Tide", "", 1, MissionArchetype.TakeAndHold, 2, GameMode.Standard);
    private static readonly Detachment AwakenedDynasty = new("d2", "Awakened Dynasty", "", 1, MissionArchetype.TakeAndHold, 2, GameMode.Standard);

    private static MergedRulesData RulesData() => new(
        "v1", DateTimeOffset.UnixEpoch,
        [Sheet("Painboy", "Orks", "orks"), Sheet("Necron Warriors", "Necrons", "necrons")],
        [],
        [
            new MergedFactionRules("orks", "Orks", [], [], [], [GreenTide]),
            new MergedFactionRules("necrons", "Necrons", [], [], [], [AwakenedDynasty]),
        ],
        []);

    private static SavedArmy Sample(string name) => new(
        Guid.NewGuid(), name, DateTimeOffset.UtcNow, "orks", "Strike Force", [], ["Green Tide"],
        [new SavedUnit(Guid.NewGuid(), "Painboy", 0, WargearSelection.Default, null, null, false, null)]);

    private static SavedArmy SampleNecron(string name) => new(
        Guid.NewGuid(), name, DateTimeOffset.UtcNow, "necrons", "Strike Force", [], ["Awakened Dynasty"],
        [new SavedUnit(Guid.NewGuid(), "Necron Warriors", 0, WargearSelection.Default, null, null, false, null)]);

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
        cut.Find(".open-build").Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Equal("orks", state.Setup.FactionId);
        Assert.EndsWith("/build", nav.Uri);
    }

    [Fact]
    public async Task Opening_a_saved_army_in_play_loads_it_and_navigates()
    {
        var store = new FakeArmyCollectionStore();
        await store.SaveAsync(Sample("My Waaagh"));
        var state = new ArmyBuilderState();

        var cut = RenderCollection(store, state);
        cut.Find(".open-play").Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Equal("orks", state.Setup.FactionId);
        Assert.EndsWith("/play", nav.Uri);
    }

    [Fact]
    public async Task A_stale_reference_via_open_in_play_shows_inline_errors_and_does_not_navigate()
    {
        var store = new FakeArmyCollectionStore();
        await store.SaveAsync(Sample("My Waaagh") with { DetachmentNames = ["Unknown Detachment"] });

        var cut = RenderCollection(store);
        cut.Find(".open-play").Click();

        Assert.Contains("Unknown detachment", cut.Find(".collection-errors").TextContent);
        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.DoesNotContain("/play", nav.Uri);
    }

    [Fact]
    public async Task A_stale_reference_via_open_in_build_shows_a_popup_first_then_still_continues_to_build()
    {
        var store = new FakeArmyCollectionStore();
        await store.SaveAsync(Sample("My Waaagh") with { DetachmentNames = ["Unknown Detachment"] });

        var cut = RenderCollection(store);
        cut.Find(".open-build").Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains("Unknown detachment", cut.Find(".modal-box").TextContent);
        Assert.DoesNotContain("/build", nav.Uri);

        cut.Find(".modal-continue").Click();

        Assert.EndsWith("/build", nav.Uri);
    }

    [Fact]
    public async Task An_army_with_a_stale_reference_is_shown_in_red()
    {
        var store = new FakeArmyCollectionStore();
        await store.SaveAsync(Sample("Broken") with { DetachmentNames = ["Unknown Detachment"] });
        await store.SaveAsync(Sample("Fine"));

        var cut = RenderCollection(store);

        var entries = cut.FindAll(".collection-entry");
        var broken = Assert.Single(entries, e => e.TextContent.Contains("Broken"));
        var fine = Assert.Single(entries, e => e.TextContent.Contains("Fine"));
        Assert.Contains("collection-entry-invalid", broken.ClassList);
        Assert.DoesNotContain("collection-entry-invalid", fine.ClassList);
    }

    [Fact]
    public async Task Opening_the_remaining_army_after_deleting_a_different_one_loads_the_right_army()
    {
        var store = new FakeArmyCollectionStore();
        var necrons = SampleNecron("Necron Army A");
        var orks = Sample("Orks Army B");
        await store.SaveAsync(necrons);
        await store.SaveAsync(orks);
        var state = new ArmyBuilderState();

        var cut = RenderCollection(store, state);

        // Delete the Necron army first, exactly like a user deleting one saved list before reopening another.
        var necronEntry = cut.FindAll(".collection-entry").Single(e => e.TextContent.Contains("Necron Army A"));
        necronEntry.QuerySelector(".delete-button")!.Click();
        cut.Find(".confirm-delete-yes").Click();
        Assert.Single(store.Armies);

        cut.Find(".open-play").Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Equal("orks", state.Setup.FactionId);
        Assert.Equal("Painboy", state.Units.Single().Datasheet.Name);
        Assert.EndsWith("/play", nav.Uri);
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
