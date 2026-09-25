using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.ArmyBuilder.Components;
using Servitor.App.ArmyBuilder.State;
using Servitor.App.Tests.ArmyBuilder.Catalog;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.ArmyBuilder.Components;

public class ArmyListPanelTests : BunitContext
{
    private readonly ArmyBuilderState _state = new();

    public ArmyListPanelTests() => Services.AddSingleton<IArmyBuilderState>(_state);

    private void Ready()
    {
        _state.SelectBattleSize(BattleSizes.StrikeForce);
        _state.SelectFaction("orks");
    }

    [Fact]
    public void Nothing_is_shown_before_a_faction_is_chosen()
    {
        Assert.Empty(Render<ArmyListPanel>().FindAll(".army-list"));
    }

    [Fact]
    public void An_empty_list_shows_a_hint()
    {
        Ready();

        Assert.Contains("No units yet", Render<ArmyListPanel>().Markup);
    }

    [Fact]
    public void Added_units_are_listed_with_size_and_points_and_can_be_removed()
    {
        Ready();
        _state.AddUnit(Units.Sheet("Boyz"), new UnitSize(10, 90));
        _state.AddUnit(Units.Sheet("Trukk"), new UnitSize(0, 70));
        var cut = Render<ArmyListPanel>();

        var rows = cut.FindAll(".army-unit");
        Assert.Equal(2, rows.Count);
        Assert.Contains("Boyz", rows[0].TextContent);
        Assert.Contains("10 models", rows[0].TextContent);
        Assert.Contains("90 pts", rows[0].TextContent);
        Assert.DoesNotContain("models", rows[1].TextContent);

        rows[0].QuerySelector(".remove-unit")!.Click();

        Assert.Equal(["Trukk"], cut.FindAll(".army-unit-name").Select(n => n.TextContent));
        Assert.Contains("Army list (1)", cut.Find("h2").TextContent);
    }

    [Fact]
    public void The_list_updates_when_units_are_added_elsewhere()
    {
        Ready();
        var cut = Render<ArmyListPanel>();

        _state.AddUnit(Units.Sheet("Boyz"), new UnitSize(10, 90));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".army-unit")));
    }

    // ---- enhancements ------------------------------------------------------------------------------------------

    private static readonly Enhancement Kit = new("e1", "Kit", "", "", 25, "Blitz", false, null);
    private static readonly Enhancement Boost = new("e2", "Boost", "", "", 10, "Blitz", true, null);

    [Fact]
    public void A_character_can_take_any_enhancement_and_its_cost_is_added_to_the_units_points()
    {
        Ready();
        _state.AddUnit(Units.Sheet("Warboss", ["Character"], sizes: [new UnitSize(1, 80)]), new UnitSize(1, 80));
        var cut = Render<ArmyListPanel>(p => p.Add(x => x.Enhancements, [Kit, Boost]));

        Assert.Equal(["(none)", "Kit (25 pts)", "Boost (10 pts, Upgrade)"], cut.FindAll(".enhancement-select option").Select(o => o.TextContent.Trim()));

        cut.Find(".enhancement-select").Change("e1");

        Assert.Contains("105 pts", cut.Find(".army-unit-points").TextContent);
        Assert.Equal("Kit", _state.Units[0].Enhancement!.Name);

        cut.Find(".enhancement-select").Change("");
        Assert.Null(_state.Units[0].Enhancement);
    }

    [Fact]
    public void Other_units_only_get_upgrades_and_epic_heroes_no_enhancement_at_all()
    {
        Ready();
        _state.AddUnit(Units.Sheet("Boyz", ["Infantry", "Battleline"]), new UnitSize(10, 90));
        _state.AddUnit(Units.Sheet("Ghazghkull", ["Character", "Epic Hero"]), new UnitSize(1, 235));
        var cut = Render<ArmyListPanel>(p => p.Add(x => x.Enhancements, [Kit, Boost]));

        var selects = cut.FindAll(".enhancement-select");

        Assert.Single(selects);
        Assert.Equal(["(none)", "Boost (10 pts, Upgrade)"], selects[0].QuerySelectorAll("option").Select(o => o.TextContent.Trim()));
    }

    [Fact]
    public void Without_selected_detachments_there_are_no_enhancements_to_pick()
    {
        Ready();
        _state.AddUnit(Units.Sheet("Warboss", ["Character"]), new UnitSize(1, 80));

        Assert.Empty(Render<ArmyListPanel>().FindAll(".enhancement-select"));
    }

    [Fact]
    public void A_broken_composition_rule_shows_up_as_its_own_issue_in_the_summary()
    {
        Ready();
        var boss = Units.Sheet("Warboss", ["Character"], sizes: [new UnitSize(1, 80)]);
        _state.AddUnit(boss, new UnitSize(1, 80));
        _state.AddUnit(boss, new UnitSize(1, 80));
        var cut = Render<ArmyListPanel>(p => p.Add(x => x.Enhancements, [Kit]));

        cut.FindAll(".enhancement-select")[0].Change("e1");
        cut.FindAll(".enhancement-select")[1].Change("e1");

        Assert.Contains("Kit is taken 2 times", cut.Find(".army-issues").TextContent);
        Assert.Contains("INVALID", cut.Find(".army-validity").TextContent);
    }
}
