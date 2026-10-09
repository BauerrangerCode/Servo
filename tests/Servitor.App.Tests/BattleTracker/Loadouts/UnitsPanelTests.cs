using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.ArmyBuilder.State;
using Servitor.App.ArmyCollection.Resolution;
using Servitor.App.ArmyImport.Parsing;
using Servitor.App.ArmyImport.Resolution;
using Servitor.App.BattleTracker.Opponent;
using Servitor.App.BattleTracker.State;
using Servitor.App.RulesData.Loading;
using Servitor.App.RulesData.Storage;
using Servitor.App.Tests.ArmyImport.Resolution;
using Servitor.App.Tests.RulesData.Loading;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.BattleTracker.Loadouts;

public class DeclaredWeaponsTests
{
    private static readonly MergedRulesData RealData = ArmyExportResolverTests.LoadRealMergedData();

    private static async Task<ArmyBuilderState> ImportAsync()
    {
        var army = new ArmyBuilderState();
        var export = WarscribeExportParser.Parse(File.ReadAllText(TestPaths.DataFile("Army1.txt"))).Export!;
        var result = await ArmyExportResolver.ResolveAsync(export, army, new BattleTrackerFixtures.FakeRulesDataProvider(RealData), new FakeDatasheetCatalog(RealData.Datasheets));
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        return army;
    }

    [Fact]
    public async Task The_import_keeps_the_weapon_counts_of_every_unit()
    {
        var army = await ImportAsync();

        var boyz = army.Units.First(u => u.Datasheet.Name == "Boyz");
        Assert.Contains(boyz.DeclaredWeapons!, w => w is { Name: "Slugga", Count: 18 });
        Assert.Contains(boyz.DeclaredWeapons!, w => w is { Name: "Kustom Choppa", Count: 2 });
        Assert.All(army.Units, u => Assert.NotEmpty(u.DeclaredWeapons!));
    }

    [Fact]
    public async Task Changing_the_wargear_in_build_drops_the_declared_weapons_of_that_unit()
    {
        var army = await ImportAsync();
        var boyz = army.Units.First(u => u.Datasheet.Name == "Boyz");

        army.SetWargear(boyz.Id, WargearSelection.Default);

        Assert.Null(army.Units.First(u => u.Id == boyz.Id).DeclaredWeapons);
    }

    [Fact]
    public async Task The_active_army_survives_a_reload_with_its_weapons_and_the_same_unit_order()
    {
        var store = new InMemoryRulesDataStore();
        var provider = new BattleTrackerFixtures.FakeRulesDataProvider(RealData);
        var catalog = new FakeDatasheetCatalog(RealData.Datasheets);
        var army = await ImportAsync();
        await new ActiveArmyStore(store, provider, catalog).SaveAsync(army);

        var restored = new ArmyBuilderState();
        var ok = await new ActiveArmyStore(store, provider, catalog).RestoreAsync(restored);

        Assert.True(ok);
        Assert.Equal(army.Units.Select(u => u.Datasheet.Name), restored.Units.Select(u => u.Datasheet.Name));
        Assert.Equal(army.Units.Select(u => u.DeclaredWeapons?.Count), restored.Units.Select(u => u.DeclaredWeapons?.Count));
        Assert.Equal(army.Units.Select(u => UnitKeysOf(army, u)), restored.Units.Select(u => UnitKeysOf(restored, u)));
    }

    [Fact]
    public async Task Restoring_without_a_saved_army_changes_nothing()
    {
        var restored = new ArmyBuilderState();

        var ok = await new ActiveArmyStore(new InMemoryRulesDataStore(), new BattleTrackerFixtures.FakeRulesDataProvider(RealData), new FakeDatasheetCatalog(RealData.Datasheets)).RestoreAsync(restored);

        Assert.False(ok);
        Assert.Empty(restored.Units);
    }

    private static string UnitKeysOf(ArmyBuilderState army, ArmyUnit unit) => Servitor.App.BattleTracker.Units.UnitKeys.Of(army.Units, unit);
}

public class UnitConditionStateTests
{
    private static readonly Servitor.App.BattleTracker.Units.ModelGroup Group = new("g0", "Standard", 3, 2, ["Slugga"]);

    private readonly InMemoryRulesDataStore _store = new();

    [Fact]
    public async Task Wounds_kills_revives_and_tokens_survive_a_reload()
    {
        var before = new UnitConditionState(_store);
        await before.AdjustDamageAsync("u|g0", Group, 3);
        await before.AddTokenAsync("u|g0");
        await before.SetCountsDownAsync("u|g0", true);
        await before.KillModelAsync("u|g0", Group);
        await before.ReviveModelAsync("u|g0", Group);

        var after = new UnitConditionState(_store);
        await after.LoadAsync();

        Assert.Equal(new Servitor.App.BattleTracker.Units.GroupCondition(Damage: 2, Tokens: 1, CountsDown: true), after.Of("u|g0"));
    }

    [Fact]
    public async Task A_group_back_to_fresh_is_forgotten_and_reset_forgets_everything()
    {
        var state = new UnitConditionState(_store);
        await state.AdjustDamageAsync("u|g0", Group, 2);
        await state.AdjustDamageAsync("u|g0", Group, -2);
        await state.AddTokenAsync("v|g0");

        await state.ResetAsync();

        Assert.Equal(Servitor.App.BattleTracker.Units.GroupCondition.Fresh, state.Of("u|g0"));
        Assert.Equal(0, state.Of("v|g0").Tokens);
    }
}

public class UnitsPanelTests : BunitContext
{
    private static readonly MergedRulesData RealData = ArmyExportResolverTests.LoadRealMergedData();

    private readonly ArmyBuilderState _army = new();

    public UnitsPanelTests()
    {
        var provider = new BattleTrackerFixtures.FakeRulesDataProvider(RealData);
        var catalog = new FakeDatasheetCatalog(RealData.Datasheets);
        var export = WarscribeExportParser.Parse(File.ReadAllText(TestPaths.DataFile("Army1.txt"))).Export!;
        ArmyExportResolver.ResolveAsync(export, _army, provider, catalog).GetAwaiter().GetResult();

        Services.AddSingleton<IRulesDataStore>(new InMemoryRulesDataStore());
        Services.AddSingleton<IBattleProgressState, BattleProgressState>();
        Services.AddSingleton<IArmyRuleState, ArmyRuleState>();
        Services.AddSingleton<IStratagemUsageState, StratagemUsageState>();
        Services.AddSingleton<IUnitConditionState, UnitConditionState>();
        Services.AddSingleton<IArmyBuilderState>(_army);
        Services.AddSingleton<IMergedRulesDataProvider>(provider);
        Services.AddSingleton<IOpponentArmyState>(new OpponentArmyState(provider, catalog));
        Services.AddSingleton<IActiveArmyStore>(sp => new ActiveArmyStore(sp.GetRequiredService<IRulesDataStore>(), provider, catalog));
    }

    private IRenderedComponent<Servitor.App.BattleTracker.Pages.Play> OpenUnits()
    {
        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();
        cut.Find(".view-units").Click();
        return cut;
    }

    private static AngleSharp.Dom.IElement Card(IRenderedComponent<Servitor.App.BattleTracker.Pages.Play> cut, string title) =>
        cut.FindAll(".unit-card").First(c => c.QuerySelector("h3")!.TextContent == title);

    private static AngleSharp.Dom.IElement WeaponRow(AngleSharp.Dom.IElement card, string weapon) =>
        card.QuerySelectorAll(".weapon-table tr").First(r => r.QuerySelector(".weapon-name")!.TextContent.StartsWith(weapon, StringComparison.Ordinal));

    [Fact]
    public void Every_unit_of_the_army_is_listed_and_a_leader_shares_the_card_of_the_unit_it_leads()
    {
        var cut = OpenUnits();

        var titles = cut.FindAll(".unit-card h3").Select(h => h.TextContent).ToList();
        Assert.Contains("Boyz + Painboy", titles);
        Assert.Contains("Boyz + Weirdboy", titles);
        Assert.Contains("Trukk", titles);
        Assert.DoesNotContain("Painboy", titles);
        var combined = Card(cut, "Boyz + Painboy");
        Assert.Equal(["Leader: Painboy", "Bodyguard: Boyz"], combined.QuerySelectorAll(".role-title").Select(t => t.TextContent.Trim()));
    }

    [Fact]
    public void Only_the_equipped_weapons_are_shown_with_one_dot_per_model_carrying_them()
    {
        var cut = OpenUnits();
        var boyz = Card(cut, "Boyz + Painboy");

        var weaponNames = boyz.QuerySelectorAll(".weapon-table .weapon-name").Select(n => n.TextContent).ToList();
        Assert.DoesNotContain(weaponNames, n => n.StartsWith("Big Choppa", StringComparison.Ordinal));
        Assert.Equal(18, WeaponRow(boyz, "Slugga").QuerySelectorAll(".dot").Length);
        Assert.Equal(2, WeaponRow(boyz, "Kustom Choppa").QuerySelectorAll(".dot").Length);
        Assert.All(boyz.QuerySelectorAll(".dot"), d => Assert.DoesNotContain("dead", d.ClassList));
    }

    [Fact]
    public void Killing_a_model_of_a_group_fades_its_dot_on_exactly_the_weapons_that_group_carries()
    {
        var cut = OpenUnits();
        var boyz = Card(cut, "Boyz + Painboy");
        var nobs = boyz.QuerySelectorAll(".model-group").First(g => g.TextContent.Contains("Kustom Choppa"));

        nobs.QuerySelector(".kill-model")!.Click();

        boyz = Card(cut, "Boyz + Painboy");
        Assert.Equal(1, WeaponRow(boyz, "Kustom Choppa").QuerySelectorAll(".dot.dead").Length);
        Assert.Equal(0, WeaponRow(boyz, "Slugga").QuerySelectorAll(".dot.dead").Length);
        Assert.Contains("20 / 21 models", boyz.QuerySelector(".unit-models")!.TextContent);
    }

    [Fact]
    public void A_destroyed_model_can_be_revived_and_wounds_can_be_healed()
    {
        var cut = OpenUnits();
        var nobs = () => Card(cut, "Boyz + Painboy").QuerySelectorAll(".model-group").First(g => g.TextContent.Contains("Kustom Choppa"));

        nobs().QuerySelector(".kill-model")!.Click();
        nobs().QuerySelector(".revive-model")!.Click();
        Assert.Empty(Card(cut, "Boyz + Painboy").QuerySelectorAll(".dot.dead"));

        nobs().QuerySelector(".wound-up")!.Click();
        Assert.Contains("Wounds taken 1 / 2", nobs().QuerySelector(".wounds-readout")!.TextContent.Replace("\n", " ").Trim());
        nobs().QuerySelector(".wound-down")!.Click();
        Assert.Contains("Wounds taken 0 / 2", nobs().QuerySelector(".wounds-readout")!.TextContent.Replace("\n", " ").Trim());
    }

    [Fact]
    public void The_count_mode_switches_between_wounds_taken_and_wounds_left_and_the_buttons_follow_it()
    {
        var cut = OpenUnits();
        var trukk = () => Card(cut, "Trukk").QuerySelector(".model-group")!;

        trukk().QuerySelector(".toggle-count-mode")!.Click();
        Assert.Contains("Wounds left", trukk().QuerySelector(".wounds-readout")!.TextContent);

        trukk().QuerySelector(".wound-down")!.Click();
        Assert.Contains("Wounds left 9 / 10", trukk().QuerySelector(".wounds-readout")!.TextContent.Replace("\n", " ").Trim());
    }

    [Fact]
    public void Destroying_the_leader_role_marks_only_that_role_and_the_unit_can_be_destroyed_and_restored()
    {
        var cut = OpenUnits();
        var card = () => Card(cut, "Boyz + Painboy");

        card().QuerySelectorAll(".role-section")[0].QuerySelector(".destroy-group")!.Click();

        Assert.Contains("role-destroyed", card().QuerySelectorAll(".role-section")[0].ClassList);
        Assert.DoesNotContain("role-destroyed", card().QuerySelectorAll(".role-section")[1].ClassList);
        Assert.DoesNotContain("destroyed", card().ClassList);

        card().QuerySelector(".destroy-unit")!.Click();
        Assert.Contains("destroyed", card().ClassList);

        card().QuerySelector(".restore-unit")!.Click();
        Assert.DoesNotContain("destroyed", card().ClassList);
        Assert.Empty(card().QuerySelectorAll(".dot.dead"));
    }

    [Fact]
    public void Tokens_are_added_and_removed_per_group()
    {
        var cut = OpenUnits();
        var group = () => Card(cut, "Trukk").QuerySelector(".model-group")!;

        group().QuerySelector(".add-token")!.Click();
        group().QuerySelector(".add-token")!.Click();
        Assert.Contains("Tokens: 2", group().TextContent);

        group().QuerySelector(".remove-token")!.Click();
        Assert.Contains("Tokens: 1", group().TextContent);
    }
}
