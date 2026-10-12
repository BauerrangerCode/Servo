using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.ArmyBuilder.State;
using Servitor.App.ArmyImport.Parsing;
using Servitor.App.BattleTracker.Opponent;
using Servitor.App.BattleTracker.State;
using Servitor.App.RulesData.Loading;
using Servitor.App.RulesData.Storage;
using Servitor.App.Tests.ArmyImport.Resolution;
using Servitor.App.Tests.RulesData.Loading;
using Servitor.RulesData.Merging;
using Servitor.App.Shared.Pager;
using Servitor.App.Shared.Viewport;
using Servitor.App.Tests.Shared.Pager;

namespace Servitor.App.Tests.BattleTracker.Opponent;

public class OpponentArmyStateTests
{
    private static readonly MergedRulesData RealData = ArmyExportResolverTests.LoadRealMergedData();

    private static OpponentArmyState NewState() =>
        new(new BattleTrackerFixtures.FakeRulesDataProvider(RealData), new FakeDatasheetCatalog(RealData.Datasheets));

    private static string Army1 => File.ReadAllText(TestPaths.DataFile("Army1.txt"));

    [Fact]
    public async Task A_pasted_export_becomes_the_opponents_army()
    {
        var state = NewState();
        Assert.False(state.HasArmy);

        var result = await state.LoadAsync(Army1);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasArmy);
        Assert.Equal(16, state.Army.Units.Count);
        Assert.Equal("orks", state.Army.Setup.FactionId);
    }

    [Fact]
    public async Task A_failed_load_reports_errors_and_keeps_the_army_that_was_loaded()
    {
        var state = NewState();
        await state.LoadAsync(Army1);

        var result = await state.LoadAsync("this is not an army list");

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(16, state.Army.Units.Count);
    }

    [Fact]
    public async Task Tokens_and_the_destroyed_mark_belong_to_a_unit_and_clearing_drops_them()
    {
        var state = NewState();
        await state.LoadAsync(Army1);
        var unit = state.Army.Units[0].Id;
        var other = state.Army.Units[1].Id;

        state.AddToken(unit);
        state.AddToken(unit);
        state.RemoveToken(unit);
        state.ToggleDestroyed(unit);

        Assert.Equal((1, true), (state.Tokens(unit), state.IsDestroyed(unit)));
        Assert.Equal((0, false), (state.Tokens(other), state.IsDestroyed(other)));

        state.ToggleDestroyed(unit);
        Assert.False(state.IsDestroyed(unit));

        state.Clear();
        Assert.False(state.HasArmy);
        Assert.Equal(0, state.Tokens(unit));
    }
}

public class OpponentArmyModalTests : BunitContext
{
    private static readonly MergedRulesData RealData = ArmyExportResolverTests.LoadRealMergedData();

    private readonly ArmyBuilderState _ownArmy = BattleTrackerFixtures.OrkArmy();

    public OpponentArmyModalTests()
    {
        var provider = new BattleTrackerFixtures.FakeRulesDataProvider(RealData);
        Services.AddSingleton<IRulesDataStore>(new InMemoryRulesDataStore());
        Services.AddSingleton<IBattleProgressState, BattleProgressState>();
        Services.AddSingleton<IPagerInterop, FakePagerInterop>();
        Services.AddSingleton<IViewportInterop, FakeViewportInterop>();
        Services.AddSingleton<IArmyRuleState, ArmyRuleState>();
        Services.AddSingleton<IStratagemUsageState, StratagemUsageState>();
        Services.AddSingleton<IUnitConditionState, UnitConditionState>();
        Services.AddSingleton<IActiveArmyStore>(sp => new ActiveArmyStore(
            sp.GetRequiredService<IRulesDataStore>(), sp.GetRequiredService<IMergedRulesDataProvider>(), new FakeDatasheetCatalog([])));
        Services.AddSingleton<IArmyBuilderState>(_ownArmy);
        Services.AddSingleton<IMergedRulesDataProvider>(provider);
        Services.AddSingleton<IOpponentArmyState>(new OpponentArmyState(provider, new FakeDatasheetCatalog(RealData.Datasheets)));
    }

    private IRenderedComponent<Servitor.App.BattleTracker.Pages.Play> OpenModal()
    {
        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();
        cut.Find(".open-opponent").Click();
        return cut;
    }

    private static void Load(IRenderedComponent<Servitor.App.BattleTracker.Pages.Play> cut)
    {
        cut.Find(".opponent-textarea").Input(File.ReadAllText(TestPaths.DataFile("Army1.txt")));
        cut.Find(".load-opponent").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".opponent-unit")), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void The_modal_opens_over_the_play_screen_with_a_paste_field_and_closes_again()
    {
        var cut = OpenModal();

        Assert.NotNull(cut.Find(".modal-dialog"));
        Assert.NotNull(cut.Find(".opponent-textarea"));
        Assert.Empty(cut.FindAll(".clear-opponent"));
        Assert.NotNull(cut.Find(".battle-progress"));

        cut.Find(".modal-close").Click();

        Assert.Empty(cut.FindAll(".modal-dialog"));
    }

    [Fact]
    public void A_bad_paste_shows_errors_and_loads_nothing()
    {
        var cut = OpenModal();

        cut.Find(".opponent-textarea").Input("nonsense");
        cut.Find(".load-opponent").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".opponent-errors li")));
        Assert.Empty(cut.FindAll(".opponent-unit"));
    }

    [Fact]
    public void The_roster_is_compact_with_badges_pills_model_count_and_leader_and_no_weapons()
    {
        var cut = OpenModal();
        Load(cut);

        Assert.Equal(16, cut.FindAll(".opponent-unit").Count);
        var boyz = cut.FindAll(".opponent-unit").First(u => u.QuerySelector(".unit-name")!.TextContent == "Boyz" && u.TextContent.Contains("Led by Painboy"));
        Assert.Contains("20 models", boyz.QuerySelector(".models-badge")!.TextContent);
        Assert.Contains("Led by Painboy", boyz.QuerySelector(".leader-badge")!.TextContent);
        Assert.Contains(boyz.QuerySelectorAll(".stat").Select(s => s.TextContent), t => t.StartsWith("T "));
        Assert.Contains(boyz.QuerySelectorAll(".keyword-pill").Select(p => p.TextContent), t => t.Equals("Infantry", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Choppa", boyz.TextContent);
        Assert.Contains("Battleline", boyz.QuerySelector(".role-pill")!.TextContent);
        Assert.DoesNotContain(boyz.QuerySelectorAll(".keyword-pill").Select(p => p.TextContent), t => t.Equals("Battleline", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Tokens_and_destroyed_are_marked_per_unit_without_touching_the_own_army()
    {
        var cut = OpenModal();
        Load(cut);
        var ownUnits = _ownArmy.Units.Count;

        cut.FindAll(".opponent-unit")[0].QuerySelector(".add-token")!.Click();
        cut.FindAll(".opponent-unit")[0].QuerySelector(".add-token")!.Click();
        cut.FindAll(".opponent-unit")[1].QuerySelector(".toggle-destroyed")!.Click();

        Assert.Contains("Tokens: 2", cut.FindAll(".opponent-unit")[0].TextContent);
        Assert.Contains("destroyed", cut.FindAll(".opponent-unit")[1].ClassList);
        Assert.DoesNotContain("destroyed", cut.FindAll(".opponent-unit")[0].ClassList);
        Assert.Equal(ownUnits, _ownArmy.Units.Count);
    }

    [Fact]
    public void The_army_rules_section_is_collapsible_and_clearing_returns_to_the_paste_field()
    {
        var cut = OpenModal();
        Load(cut);

        Assert.NotEmpty(cut.FindAll("details.opponent-rules .rule-block"));
        Assert.NotEmpty(cut.FindAll("details.opponent-rules .tag-timing"));

        cut.Find(".clear-opponent").Click();

        Assert.Empty(cut.FindAll(".opponent-unit"));
        Assert.NotNull(cut.Find(".opponent-textarea"));
    }

    [Fact]
    public void The_commands_view_lists_every_standard_stratagem_of_the_armys_detachments_unfiltered()
    {
        Services.AddSingleton<IMergedRulesDataProvider>(new BattleTrackerFixtures.FakeRulesDataProvider(BattleTrackerFixtures.Data()));
        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".commands .stratagem-card")));
        var names = cut.FindAll(".commands .stratagem-card h3").Select(h => h.TextContent.Trim()).ToList();
        Assert.Contains("GRENADE", names);
        Assert.Contains("FIRE OVERWATCH", names);
        Assert.Contains("SMOKESCREEN", names);
        Assert.Contains("BIG KRUNCH", names);
        Assert.DoesNotContain("BOARDING ONLY", names);
        Assert.DoesNotContain("OTHER DETACHMENT", names);
        Assert.Empty(cut.FindAll(".commands .use-stratagem"));
        Assert.NotNull(cut.Find(".action-queue"));
    }
}
