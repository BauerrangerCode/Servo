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
}
