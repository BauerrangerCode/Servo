using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.ArmyBuilder.Components;
using Servitor.App.ArmyBuilder.State;
using Servitor.App.Tests.ArmyBuilder.Catalog;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.ArmyBuilder.Components;

public class ArmySummaryTests : BunitContext
{
    private readonly ArmyBuilderState _state = new();

    private static readonly Datasheet Boyz = Units.Sheet("Boyz", wounds: "1", sizes: [new UnitSize(10, 90), new UnitSize(20, 180)]);
    private static readonly Datasheet Warboss = Units.Sheet("Warboss", wounds: "5", sizes: [new UnitSize(1, 80)]);
    private static readonly Datasheet Titan = Units.Sheet("Stompa", wounds: "60", sizes: [new UnitSize(1, 900)]);

    public ArmySummaryTests()
    {
        Services.AddSingleton<IArmyBuilderState>(_state);
        _state.SelectBattleSize(BattleSizes.Incursion);
        _state.SelectFaction("orks");
    }

    private string Total(IRenderedComponent<ArmySummary> cut, string name) => cut.Find($".army-total-{name} dd").TextContent;

    [Fact]
    public void An_empty_army_shows_zero_totals_against_the_budget_and_is_valid()
    {
        var cut = Render<ArmySummary>();

        Assert.Equal("0 / 1000", Total(cut, "points"));
        Assert.Equal("0", Total(cut, "units"));
        Assert.Equal("VALID", cut.Find(".army-validity").TextContent.Trim());
        Assert.Empty(cut.FindAll(".army-issues"));
    }

    [Fact]
    public void Totals_update_immediately_on_every_add_and_remove()
    {
        var cut = Render<ArmySummary>();

        _state.AddUnit(Boyz, new UnitSize(10, 90));
        _state.AddUnit(Warboss, new UnitSize(1, 80));
        cut.WaitForAssertion(() => Assert.Equal("170 / 1000", Total(cut, "points")));
        Assert.Equal("2", Total(cut, "units"));
        Assert.Equal("11", Total(cut, "models"));
        Assert.Equal("15", Total(cut, "wounds"));

        _state.RemoveUnit(_state.Units[0].Id);
        cut.WaitForAssertion(() => Assert.Equal("80 / 1000", Total(cut, "points")));
        Assert.Equal("1", Total(cut, "models"));
    }

    [Fact]
    public void Exceeding_the_budget_flips_the_indicator_to_invalid_with_a_visible_reason()
    {
        var cut = Render<ArmySummary>();

        _state.AddUnit(Titan, new UnitSize(1, 900));
        _state.AddUnit(Warboss, new UnitSize(1, 80));
        _state.AddUnit(Warboss, new UnitSize(1, 80));

        cut.WaitForAssertion(() => Assert.Contains("INVALID – 1 issue", cut.Find(".army-validity").TextContent));
        Assert.Contains("1060 pts, 60 over the 1000 pts limit of Incursion", cut.Find(".army-issues").TextContent);
        Assert.Contains("over-budget", cut.Find(".army-total-points").ClassName);
        Assert.Contains("invalid", cut.Find(".army-validity").ClassName);
    }

    [Fact]
    public void Removing_a_unit_makes_the_army_valid_again()
    {
        var cut = Render<ArmySummary>();
        _state.AddUnit(Titan, new UnitSize(1, 900));
        _state.AddUnit(Boyz, new UnitSize(10, 90));
        _state.AddUnit(Warboss, new UnitSize(1, 80));
        cut.WaitForAssertion(() => Assert.Contains("INVALID", cut.Find(".army-validity").TextContent));

        _state.RemoveUnit(_state.Units[2].Id);

        cut.WaitForAssertion(() => Assert.Equal("VALID", cut.Find(".army-validity").TextContent.Trim()));
    }

    [Fact]
    public void A_bigger_battle_size_can_make_a_too_expensive_army_valid()
    {
        var cut = Render<ArmySummary>();
        _state.AddUnit(Titan, new UnitSize(1, 900));
        _state.AddUnit(Boyz, new UnitSize(20, 180));
        cut.WaitForAssertion(() => Assert.Contains("INVALID", cut.Find(".army-validity").TextContent));

        _state.SelectBattleSize(BattleSizes.StrikeForce);

        cut.WaitForAssertion(() => Assert.Equal("VALID", cut.Find(".army-validity").TextContent.Trim()));
        Assert.Equal("1080 / 2000", Total(cut, "points"));
    }
}
