using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.ArmyBuilder.Pages;
using Servitor.App.ArmyBuilder.Rules;
using Servitor.App.Tests.ArmyBuilder.Rules;
using Servitor.App.ArmyBuilder.State;
using Servitor.App.ArmyCollection.State;
using Servitor.App.RulesData.Loading;
using Servitor.App.Tests.ArmyCollection;
using Servitor.App.Tests.RulesData.Loading;
using Servitor.App.Shared.Backdrop;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.ArmyBuilder.Pages;

public class BuildTests : BunitContext
{
    private readonly FactionBackdrop _backdrop = new();
    private readonly ArmyBuilderState _state = new();
    private readonly FakeRuleProvider _rules = new();
    private readonly FakeArmyCollectionStore _collectionStore = new();

    public BuildTests()
    {
        Services.AddSingleton<IFactionBackdrop>(_backdrop);
        Services.AddSingleton<IDatasheetCatalog>(new FakeDatasheetCatalog());
        Services.AddSingleton<IDetachmentRuleProvider>(_rules);
        Services.AddSingleton<IArmyCollectionStore>(_collectionStore);
    }

    private sealed class FakeProvider(MergedRulesData? data) : IMergedRulesDataProvider
    {
        public event Action? DataChanged
        {
            add { }
            remove { }
        }

        public Task<MergedRulesData?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(data);

        public Task StoreAsync(MergedRulesData value, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static MergedRulesData Data() => new(
        "v1",
        DateTimeOffset.UnixEpoch,
        [],
        [],
        [
            new MergedFactionRules("orks", "Orks", [], [],
            [
                new Enhancement("e1", "Da Biggest Boss", "Flavor.", "The bearer has an <b>Invuln save</b>.", 15, "Waaagh Tribe", false, null),
            ],
            [
                new Detachment("d1", "Waaagh Tribe", "Waaagh Tribe rules text.", 3, MissionArchetype.TakeAndHold, 4, GameMode.Standard)
                {
                    RuleText = "**Da Biggest and da Best**\n\nThe actual rule text.",
                },
                new Detachment("d2", "Blitz Brigade", "", 1, MissionArchetype.PurgeTheFoe, 2, GameMode.Standard),
                new Detachment("d3", "Boarding Thing", "", 0, null, 0, GameMode.BoardingActions),
            ]),
        ],
        []);

    private IRenderedComponent<Build> RenderBuild(MergedRulesData? data)
    {
        Services.AddSingleton<IMergedRulesDataProvider>(new FakeProvider(data));
        Services.AddSingleton<IArmyBuilderState>(_state);
        return Render<Build>();
    }

    [Fact]
    public void Faction_and_detachment_steps_only_appear_after_the_previous_choice()
    {
        var cut = RenderBuild(Data());

        Assert.Empty(cut.FindAll(".faction-select"));

        cut.FindAll(".chip")[1].Click();
        Assert.Single(cut.FindAll(".faction-select"));
        Assert.Empty(cut.FindAll(".detachment-list"));

        cut.Find(".faction-select").Change("orks");
        Assert.Single(cut.FindAll(".detachment-list"));
    }

    [Fact]
    public void Choosing_a_faction_makes_it_the_backdrop()
    {
        var cut = RenderBuild(Data());
        cut.FindAll(".chip")[1].Click();

        cut.Find(".faction-select").Change("orks");

        Assert.Equal("orks", _backdrop.FactionSlug);
    }

    [Fact]
    public void Detachments_show_dp_tag_and_enhancement_count_and_hide_non_standard_modes()
    {
        var cut = RenderBuild(Data());
        cut.FindAll(".chip")[1].Click();
        cut.Find(".faction-select").Change("orks");

        var rows = cut.FindAll(".detachment");
        Assert.Equal(2, rows.Count);
        Assert.Contains("3 DP", rows[0].TextContent);
        Assert.Contains("Take and Hold", rows[0].TextContent);
        Assert.Contains("4 enh", rows[0].TextContent);
        Assert.DoesNotContain("Boarding Thing", cut.Markup);
    }

    [Fact]
    public void Clicking_a_detachment_name_toggles_its_flavor_text_and_its_actual_rule_text()
    {
        var cut = RenderBuild(Data());
        cut.FindAll(".chip")[1].Click();
        cut.Find(".faction-select").Change("orks");

        Assert.Empty(cut.FindAll(".detachment-rules"));

        cut.FindAll(".detachment-name")[0].Click();
        var rules = cut.Find(".detachment-rules");
        Assert.Contains("Waaagh Tribe rules text.", rules.TextContent);
        Assert.Contains("Da Biggest and da Best", rules.TextContent);
        Assert.Contains("The actual rule text.", rules.TextContent);

        cut.FindAll(".detachment-name")[0].Click();
        Assert.Empty(cut.FindAll(".detachment-rules"));
    }

    [Fact]
    public void A_detachment_with_no_rule_text_shows_only_the_flavor_line()
    {
        var cut = RenderBuild(Data());
        cut.FindAll(".chip")[1].Click();
        cut.Find(".faction-select").Change("orks");

        cut.FindAll(".detachment-name")[1].Click(); // "Blitz Brigade" — empty Legend, no RuleText in the fixture

        Assert.Empty(cut.Find(".detachment-rules").QuerySelectorAll(".detachment-rule-text"));
    }

    [Fact]
    public void The_detachment_rules_panel_also_lists_its_enhancements()
    {
        var cut = RenderBuild(Data());
        cut.FindAll(".chip")[1].Click();
        cut.Find(".faction-select").Change("orks");

        cut.FindAll(".detachment-name")[0].Click(); // "Waaagh Tribe" — has one enhancement in the fixture

        var enhancement = cut.Find(".detachment-enhancement");
        Assert.Contains("Da Biggest Boss", enhancement.TextContent);
        Assert.Contains("15 pts", enhancement.TextContent);
        Assert.Contains("Invuln save", enhancement.TextContent);
        Assert.Contains("<b>Invuln save</b>", enhancement.InnerHtml);
    }

    [Fact]
    public void A_detachment_with_no_enhancements_shows_no_enhancement_section()
    {
        var cut = RenderBuild(Data());
        cut.FindAll(".chip")[1].Click();
        cut.Find(".faction-select").Change("orks");

        cut.FindAll(".detachment-name")[1].Click(); // "Blitz Brigade" — no enhancements in the fixture

        Assert.Empty(cut.Find(".detachment-rules").QuerySelectorAll(".detachment-enhancements"));
    }

    [Fact]
    public void Adding_detachments_updates_the_dp_progress_and_disables_what_no_longer_fits()
    {
        var cut = RenderBuild(Data());
        cut.FindAll(".chip")[0].Click(); // Incursion: 2 DP
        cut.Find(".faction-select").Change("orks");

        Assert.False(cut.FindAll(".detachment-action")[0].HasAttribute("disabled")); // a 3 DP detachment is fine as the only one

        cut.FindAll(".detachment-action")[1].Click();

        Assert.Contains("1/2 DP", cut.Find(".dp-progress").TextContent);
        Assert.True(cut.FindAll(".detachment-action")[0].HasAttribute("disabled")); // now it no longer fits
        Assert.Equal("Remove", cut.FindAll(".detachment-action")[1].TextContent);
    }

    [Fact]
    public void Without_merged_data_a_hint_replaces_the_setup()
    {
        var cut = RenderBuild(null);

        Assert.Contains("No rules data yet", cut.Markup);
    }

    [Fact]
    public void Choosing_a_faction_hands_its_ally_factions_to_the_army_and_the_catalog_offers_them()
    {
        var data = Data() with
        {
            Factions =
            [
                Data().Factions[0] with { AllyFactionIds = ["necrons"] },
                new MergedFactionRules("necrons", "Necrons", [], [], [], []),
            ],
        };
        var cut = RenderBuild(data);
        cut.FindAll(".chip")[1].Click();

        cut.Find(".faction-select").Change("orks");

        Assert.Equal(["necrons"], _state.Setup.AllyFactionIds);
        Assert.Contains("Your army can also take: Necrons", cut.Find(".allies-note").TextContent);
    }

    [Fact]
    public void The_detachment_rules_are_loaded_when_the_page_opens()
    {
        RenderBuild(Data());

        Assert.Equal(1, _rules.Loads);
    }

    [Fact]
    public void Choosing_a_faction_defaults_the_save_name_to_the_faction_name()
    {
        var cut = RenderBuild(Data());
        cut.FindAll(".chip")[1].Click();

        cut.Find(".faction-select").Change("orks");

        Assert.Equal("Orks", cut.Find(".save-name").GetAttribute("value"));
    }

    [Fact]
    public void Saving_stores_a_snapshot_in_my_collection()
    {
        var cut = RenderBuild(Data());
        cut.FindAll(".chip")[1].Click();
        cut.Find(".faction-select").Change("orks");

        cut.Find(".save-button").Click();

        var saved = Assert.Single(_collectionStore.Armies);
        Assert.Equal("Orks", saved.Name);
        Assert.Equal("orks", saved.FactionId);
        Assert.Contains("Saved", cut.Find(".save-message").TextContent);
    }

    [Fact]
    public async Task Saving_with_a_duplicate_name_asks_before_overwriting()
    {
        var existing = new SavedArmy(Guid.NewGuid(), "Orks", DateTimeOffset.UtcNow, "orks", "Incursion", [], [], []);
        await _collectionStore.SaveAsync(existing);

        var cut = RenderBuild(Data());
        cut.FindAll(".chip")[1].Click();
        cut.Find(".faction-select").Change("orks");

        cut.Find(".save-button").Click();

        Assert.Single(_collectionStore.Armies); // not yet overwritten
        Assert.Contains("already saved", cut.Find(".modal-box").TextContent);
    }

    [Fact]
    public async Task Confirming_the_overwrite_replaces_the_existing_entry_in_place()
    {
        var existingId = Guid.NewGuid();
        var existing = new SavedArmy(existingId, "Orks", DateTimeOffset.UtcNow, "orks", "Incursion", [], [], []);
        await _collectionStore.SaveAsync(existing);

        var cut = RenderBuild(Data());
        cut.FindAll(".chip")[1].Click(); // Strike Force
        cut.Find(".faction-select").Change("orks");

        cut.Find(".save-button").Click();
        cut.Find(".modal-confirm").Click();

        var saved = Assert.Single(_collectionStore.Armies);
        Assert.Equal(existingId, saved.Id);
        Assert.Equal("Strike Force", saved.BattleSizeName);
        Assert.Contains("Saved", cut.Find(".save-message").TextContent);
    }

    [Fact]
    public async Task Cancelling_the_overwrite_leaves_the_existing_entry_untouched()
    {
        var existing = new SavedArmy(Guid.NewGuid(), "Orks", DateTimeOffset.UtcNow, "orks", "Incursion", [], [], []);
        await _collectionStore.SaveAsync(existing);

        var cut = RenderBuild(Data());
        cut.FindAll(".chip")[1].Click();
        cut.Find(".faction-select").Change("orks");

        cut.Find(".save-button").Click();
        cut.Find(".modal-cancel").Click();

        var remaining = Assert.Single(_collectionStore.Armies);
        Assert.Equal("Incursion", remaining.BattleSizeName);
        Assert.Empty(cut.FindAll(".modal-box"));
    }
}
