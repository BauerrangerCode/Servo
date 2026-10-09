using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.ArmyBuilder.Components;
using Servitor.App.ArmyBuilder.State;
using Servitor.App.RulesData.Loading;
using Servitor.App.Tests.ArmyBuilder.Catalog;
using Servitor.App.Tests.RulesData.Loading;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.ArmyBuilder.Components;

public class UnitCatalogPanelTests : BunitContext
{
    private readonly ArmyBuilderState _state = new();

    private static readonly Datasheet[] OrkUnits =
    [
        Units.Sheet("Boyz", ["Infantry", "Battleline"], sizes: [new UnitSize(10, 90), new UnitSize(20, 180)], constraint: new CompositionConstraint(3, 2)),
        Units.Sheet("Warboss", ["Character", "Infantry"], sizes: [new UnitSize(1, 80)], constraint: new CompositionConstraint(3, 3)),
        Units.Sheet("Ghazghkull Thraka", ["Character", "Epic Hero"], sizes: [new UnitSize(1, 235)], constraint: new CompositionConstraint(1, null)),
        Units.Sheet("Trukk", ["Vehicle", "Dedicated Transport"], points: 70),
        Units.Sheet("Old Warboss [Legends]", ["Character"]),
        Units.Sheet("Cadian Shock Troops", ["Infantry", "Battleline"], factions: ["astra-militarum"]),
    ];

    public UnitCatalogPanelTests()
    {
        Services.AddSingleton<IArmyBuilderState>(_state);
        Services.AddSingleton<IDatasheetCatalog>(new FakeDatasheetCatalog(OrkUnits));
    }

    private IRenderedComponent<UnitCatalogPanel> RenderReady(BattleSize? size = null)
    {
        _state.SelectBattleSize(size ?? BattleSizes.StrikeForce);
        _state.SelectFaction("orks");
        return Render<UnitCatalogPanel>();
    }

    private static AngleSharp.Dom.IElement Row(IRenderedComponent<UnitCatalogPanel> cut, string name) =>
        cut.FindAll(".unit-row").Single(r => r.QuerySelector(".unit-name")!.TextContent == name);

    [Fact]
    public void Nothing_is_shown_before_battle_size_and_faction_are_chosen()
    {
        var cut = Render<UnitCatalogPanel>();

        Assert.Empty(cut.FindAll(".unit-catalog"));
    }

    [Fact]
    public void Units_of_the_faction_are_grouped_by_role_with_counts_and_other_armies_units_are_left_out()
    {
        var cut = RenderReady();

        Assert.Equal(
            ["Epic Hero (1)", "Character (1)", "Battleline (1)", "Transport (1)"],
            cut.FindAll(".role-heading").Select(h => h.TextContent));
        Assert.DoesNotContain("Cadian Shock Troops", cut.Markup);
        Assert.DoesNotContain("Old Warboss", cut.Markup);
        Assert.Contains("Search 4 units", cut.Find(".catalog-search").GetAttribute("placeholder"));
    }

    [Fact]
    public void A_row_shows_model_count_or_range_and_points()
    {
        var cut = RenderReady();

        Assert.Contains("10-20 models", Row(cut, "Boyz").TextContent);
        Assert.Contains("90 pts", Row(cut, "Boyz").TextContent);
        Assert.Contains("1 models", Row(cut, "Warboss").TextContent);
        Assert.DoesNotContain("models", Row(cut, "Trukk").TextContent);
        Assert.Contains("70 pts", Row(cut, "Trukk").TextContent);
    }

    [Fact]
    public void Only_units_with_several_sizes_get_a_size_toggle_that_changes_the_points()
    {
        var cut = RenderReady();

        Assert.Empty(Row(cut, "Warboss").QuerySelectorAll(".size-toggle"));

        var toggles = Row(cut, "Boyz").QuerySelectorAll(".size-toggle .chip");
        Assert.Equal(["10", "20"], toggles.Select(t => t.TextContent));
        toggles[1].Click();

        Assert.Contains("180 pts", Row(cut, "Boyz").TextContent);
    }

    [Fact]
    public void Adding_a_unit_uses_the_selected_size()
    {
        var cut = RenderReady();
        Row(cut, "Boyz").QuerySelectorAll(".size-toggle .chip")[1].Click();

        Row(cut, "Boyz").QuerySelector(".add-unit")!.Click();

        var unit = Assert.Single(_state.Units);
        Assert.Equal(new UnitSize(20, 180), unit.Size);
    }

    [Fact]
    public void The_row_shows_what_the_next_copy_costs_when_the_price_rises_with_the_number_of_copies()
    {
        var boyz = Units.Sheet(
            "Boyz",
            ["Battleline"],
            sizes: [new UnitSize(10, 90)],
            brackets: [new CostBracket(1, 1, [new UnitSize(10, 90)]), new CostBracket(2, null, [new UnitSize(10, 100)])]);
        Services.AddSingleton<IDatasheetCatalog>(new FakeDatasheetCatalog([boyz]));
        var cut = RenderReady();
        Assert.Contains("90 pts", Row(cut, "Boyz").TextContent);

        Row(cut, "Boyz").QuerySelector(".add-unit")!.Click();

        Assert.Contains("100 pts", Row(cut, "Boyz").TextContent);
    }

    [Fact]
    public void The_limit_badge_shows_the_live_count_and_the_add_button_stops_at_the_limit()
    {
        var cut = RenderReady(BattleSizes.Incursion); // Boyz: 2 in Incursion
        Assert.Empty(Row(cut, "Boyz").QuerySelectorAll(".unit-limit"));

        Row(cut, "Boyz").QuerySelector(".add-unit")!.Click();
        Assert.Equal("×1/2", Row(cut, "Boyz").QuerySelector(".unit-limit")!.TextContent);
        Assert.False(Row(cut, "Boyz").QuerySelector(".add-unit")!.HasAttribute("disabled"));

        Row(cut, "Boyz").QuerySelector(".add-unit")!.Click();
        Assert.Equal("×2/2", Row(cut, "Boyz").QuerySelector(".unit-limit")!.TextContent);
        Assert.Contains("at-limit", Row(cut, "Boyz").QuerySelector(".unit-limit")!.ClassName);
        Assert.True(Row(cut, "Boyz").QuerySelector(".add-unit")!.HasAttribute("disabled"));
    }

    [Fact]
    public void The_search_narrows_the_list_and_the_role_tabs_show_one_group()
    {
        var cut = RenderReady();

        cut.Find(".catalog-search").Input("boy");
        Assert.Equal(["Boyz"], cut.FindAll(".unit-name").Select(n => n.TextContent));

        cut.Find(".catalog-search").Input("");
        cut.FindAll(".role-tabs .chip").Single(c => c.TextContent == "Transport").Click();
        Assert.Equal(["Trukk"], cut.FindAll(".unit-name").Select(n => n.TextContent));
    }

    [Fact]
    public void The_legends_toggle_switches_to_the_legends_units()
    {
        var cut = RenderReady();

        cut.Find(".legends-toggle").Click();

        Assert.Equal(["Old Warboss [Legends]"], cut.FindAll(".unit-name").Select(n => n.TextContent));
        Assert.Equal("Legends", cut.Find(".legends-toggle").TextContent);
    }

    [Fact]
    public void Clicking_a_unit_name_opens_its_datasheet()
    {
        var cut = RenderReady();
        Assert.Empty(cut.FindAll(".datasheet-detail"));

        Row(cut, "Warboss").QuerySelector(".unit-name")!.Click();

        Assert.Contains("Warboss", cut.Find(".datasheet-detail").TextContent);
    }

    [Fact]
    public void The_catalog_follows_the_selected_faction()
    {
        var cut = RenderReady();

        _state.SelectFaction("astra-militarum");

        cut.WaitForAssertion(() => Assert.Equal(["Cadian Shock Troops"], cut.FindAll(".unit-name").Select(n => n.TextContent)));
    }

    [Fact]
    public void A_faction_without_units_shows_a_hint()
    {
        _state.SelectBattleSize(BattleSizes.StrikeForce);
        _state.SelectFaction("necrons");
        var cut = Render<UnitCatalogPanel>();

        Assert.Contains("No units match.", cut.Markup);
    }
}
