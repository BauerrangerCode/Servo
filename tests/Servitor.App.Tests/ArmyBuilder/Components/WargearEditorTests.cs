using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.ArmyBuilder.Components;
using Servitor.App.ArmyBuilder.State;
using Servitor.App.Tests.ArmyBuilder.Catalog;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.ArmyBuilder.Components;

public class WargearEditorTests : BunitContext
{
    private readonly ArmyBuilderState _state = new();

    private static readonly WargearModel Sergeant = new(
        "Sergeant",
        [new WargearSlot("Weapon 1", ["Bolt rifle", "Plasma pistol"], 0), new WargearSlot("Weapon 2", ["Chainsword", "Power fist"], 0)]);

    private static readonly WargearPool BigShoota = new(
        "Big Shoota",
        "Shoota",
        [new WargearLimit(1, [new LimitAdjustment(true, 2, [new ModelCountCondition(ModelCountOperator.AtLeast, 20)])])]);

    private static readonly Datasheet Squad = Units.Sheet("Boyz", ["Battleline"], sizes: [new UnitSize(10, 90), new UnitSize(20, 180)]) with
    {
        Wargear = new WargearOptions([Sergeant], [BigShoota]),
        WargearCosts = [new WargearCost("Power fist", 10), new WargearCost("Big Shoota", 5)],
    };

    private static readonly Datasheet Rhino = Units.Sheet("Rhino", ["Vehicle"], points: 75);

    public WargearEditorTests()
    {
        Services.AddSingleton<IArmyBuilderState>(_state);
        _state.SelectBattleSize(BattleSizes.StrikeForce);
        _state.SelectFaction("orks");
    }

    private IRenderedComponent<ArmyListPanel> RenderList(Datasheet datasheet, UnitSize size)
    {
        _state.AddUnit(datasheet, size);
        return Render<ArmyListPanel>();
    }

    [Fact]
    public void A_unit_without_wargear_options_has_no_wargear_entry_point()
    {
        var cut = RenderList(Rhino, new UnitSize(1, 75));

        Assert.Empty(cut.FindAll(".edit-wargear"));
        Assert.Empty(cut.FindAll(".army-unit-loadout"));
    }

    [Fact]
    public void A_unit_with_options_shows_its_default_loadout_and_a_wargear_button_that_opens_the_editor()
    {
        var cut = RenderList(Squad, new UnitSize(10, 90));

        Assert.Equal("Bolt rifle, Chainsword", cut.Find(".army-unit-loadout").TextContent.Trim());
        Assert.Empty(cut.FindAll(".wargear-editor"));

        cut.Find(".edit-wargear").Click();
        Assert.Single(cut.FindAll(".wargear-editor"));

        cut.Find(".edit-wargear").Click();
        Assert.Empty(cut.FindAll(".wargear-editor"));
    }

    [Fact]
    public void The_slots_of_a_unique_model_are_radio_groups_with_the_default_checked()
    {
        var cut = RenderList(Squad, new UnitSize(10, 90));
        cut.Find(".edit-wargear").Click();

        var groups = cut.FindAll(".wargear-slot");
        Assert.Equal(2, groups.Count);
        Assert.Equal(["Bolt rifle", "Plasma pistol"], groups[0].QuerySelectorAll(".wargear-choice").Select(c => c.TextContent.Trim()));
        Assert.True(groups[0].QuerySelectorAll("input")[0].HasAttribute("checked"));
        Assert.False(groups[0].QuerySelectorAll("input")[1].HasAttribute("checked"));
        Assert.Contains("Sergeant", cut.Find(".wargear-model legend").TextContent);
    }

    [Fact]
    public void Picking_a_weapon_updates_the_loadout_and_the_points_of_the_unit_and_the_army()
    {
        var cut = RenderList(Squad, new UnitSize(10, 90));
        cut.Find(".edit-wargear").Click();

        cut.FindAll(".wargear-slot")[1].QuerySelectorAll("input")[1].Change(true); // Power fist, +10 pts

        Assert.Equal("Bolt rifle, Power fist (wargear +10 pts)", cut.Find(".army-unit-loadout").TextContent.Trim());
        Assert.Contains("100 pts", cut.Find(".army-unit-points").TextContent);
        Assert.Contains("100 / 2000", cut.Find(".army-total-points dd").TextContent);
    }

    [Fact]
    public void A_price_is_shown_next_to_the_item_that_has_one()
    {
        var cut = RenderList(Squad, new UnitSize(10, 90));
        cut.Find(".edit-wargear").Click();

        Assert.Contains("Power fist (+10 pts)", cut.FindAll(".wargear-slot")[1].TextContent);
    }

    [Fact]
    public void A_pool_is_a_stepper_that_stops_at_the_cap_of_the_unit_size()
    {
        var cut = RenderList(Squad, new UnitSize(10, 90));
        cut.Find(".edit-wargear").Click();

        Assert.Equal("0/1", cut.Find(".wargear-count").TextContent);
        Assert.True(cut.Find(".wargear-minus").HasAttribute("disabled"));

        cut.Find(".wargear-plus").Click();

        Assert.Equal("1/1", cut.Find(".wargear-count").TextContent);
        Assert.True(cut.Find(".wargear-plus").HasAttribute("disabled"));
        Assert.Contains("Replace Shoota with: Big Shoota (+5 pts each)", cut.Find(".wargear-pool-name").TextContent);
        Assert.Contains("1× Big Shoota", cut.Find(".army-unit-loadout").TextContent);
        Assert.Contains("wargear +5 pts", cut.Find(".army-unit-loadout").TextContent);
    }

    [Fact]
    public void A_bigger_unit_allows_more_models_to_take_the_item()
    {
        var cut = RenderList(Squad, new UnitSize(20, 180));
        cut.Find(".edit-wargear").Click();

        Assert.Equal("0/2", cut.Find(".wargear-count").TextContent);
    }

    [Fact]
    public void Each_unit_keeps_its_own_wargear()
    {
        _state.AddUnit(Squad, new UnitSize(10, 90));
        var cut = RenderList(Squad, new UnitSize(10, 90));
        cut.FindAll(".edit-wargear")[1].Click();

        cut.Find(".wargear-plus").Click();

        var loadouts = cut.FindAll(".army-unit-loadout").Select(l => l.TextContent.Trim()).ToList();
        Assert.DoesNotContain("Big Shoota", loadouts[0]);
        Assert.Contains("1× Big Shoota", loadouts[1]);
    }
}
