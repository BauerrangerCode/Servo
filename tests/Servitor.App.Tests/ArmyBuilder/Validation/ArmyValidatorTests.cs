using Servitor.App.ArmyBuilder.State;
using Servitor.App.ArmyBuilder.Validation;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.ArmyBuilder.Validation;

public class ArmyValidatorTests
{
    private static ArmySetup Setup(BattleSize? size) => ArmySetup.Empty with { BattleSize = size };

    private static ArmyTotals Points(int points) => new(points, 1, 1, 1);

    [Fact]
    public void An_army_within_the_budget_has_no_issues()
    {
        Assert.Empty(ArmyValidator.Validate(Setup(BattleSizes.StrikeForce), Points(2000)));
    }

    [Fact]
    public void Exceeding_the_points_budget_is_reported_with_the_amount_and_the_limit()
    {
        var issue = Assert.Single(ArmyValidator.Validate(Setup(BattleSizes.Incursion), Points(1035)));

        Assert.Equal(ArmyIssueKind.OverPointsBudget, issue.Kind);
        Assert.Equal("The army costs 1035 pts, 35 over the 1000 pts limit of Incursion.", issue.Message);
    }

    [Fact]
    public void Each_battle_size_has_its_own_limit()
    {
        Assert.NotEmpty(ArmyValidator.Validate(Setup(BattleSizes.Incursion), Points(1001)));
        Assert.Empty(ArmyValidator.Validate(Setup(BattleSizes.StrikeForce), Points(1001)));
        Assert.Empty(ArmyValidator.Validate(Setup(BattleSizes.Onslaught), Points(3000)));
        Assert.NotEmpty(ArmyValidator.Validate(Setup(BattleSizes.Onslaught), Points(3001)));
    }

    [Fact]
    public void Without_a_battle_size_there_is_no_budget_to_exceed()
    {
        Assert.Empty(ArmyValidator.Validate(Setup(null), Points(99999)));
    }
}
