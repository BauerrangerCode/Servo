using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.ArmyBuilder.Pages;
using Servitor.App.ArmyBuilder.Rules;
using Servitor.App.Tests.ArmyBuilder.Rules;
using Servitor.App.ArmyBuilder.State;
using Servitor.App.RulesData.Loading;
using Servitor.App.Tests.RulesData.Loading;
using Servitor.App.Shared.Backdrop;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.ArmyBuilder.Pages;

public class BuildTests : BunitContext
{
    private readonly FactionBackdrop _backdrop = new();
    private readonly FakeRuleProvider _rules = new();

    public BuildTests()
    {
        Services.AddSingleton<IFactionBackdrop>(_backdrop);
        Services.AddSingleton<IDatasheetCatalog>(new FakeDatasheetCatalog());
        Services.AddSingleton<IDetachmentRuleProvider>(_rules);
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
            new MergedFactionRules("orks", "Orks", [], [], [],
            [
                new Detachment("d1", "Waaagh Tribe", "", 3, MissionArchetype.TakeAndHold, 4, GameMode.Standard),
                new Detachment("d2", "Blitz Brigade", "", 1, MissionArchetype.PurgeTheFoe, 2, GameMode.Standard),
                new Detachment("d3", "Boarding Thing", "", 0, null, 0, GameMode.BoardingActions),
            ]),
        ],
        []);

    private IRenderedComponent<Build> RenderBuild(MergedRulesData? data)
    {
        Services.AddSingleton<IMergedRulesDataProvider>(new FakeProvider(data));
        Services.AddSingleton<IArmyBuilderState>(new ArmyBuilderState());
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
    public void Adding_detachments_updates_the_dp_progress_and_disables_what_no_longer_fits()
    {
        var cut = RenderBuild(Data());
        cut.FindAll(".chip")[0].Click(); // Incursion: 2 DP
        cut.Find(".faction-select").Change("orks");

        Assert.False(cut.FindAll(".detachment button")[0].HasAttribute("disabled")); // a 3 DP detachment is fine as the only one

        cut.FindAll(".detachment button")[1].Click();

        Assert.Contains("1/2 DP", cut.Find(".dp-progress").TextContent);
        Assert.True(cut.FindAll(".detachment button")[0].HasAttribute("disabled")); // now it no longer fits
        Assert.Equal("Remove", cut.FindAll(".detachment button")[1].TextContent);
    }

    [Fact]
    public void Skip_for_now_leaves_the_setup_and_can_be_resumed()
    {
        var cut = RenderBuild(Data());

        cut.Find(".skip-setup").Click();
        Assert.Contains("Army setup skipped", cut.Markup);

        cut.Find("button").Click();
        Assert.Single(cut.FindAll(".skip-setup"));
    }

    [Fact]
    public void Without_merged_data_a_hint_replaces_the_setup()
    {
        var cut = RenderBuild(null);

        Assert.Contains("No rules data yet", cut.Markup);
    }

    [Fact]
    public void The_detachment_rules_are_loaded_when_the_page_opens()
    {
        RenderBuild(Data());

        Assert.Equal(1, _rules.Loads);
    }
}
