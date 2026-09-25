using Servitor.App.ArmyBuilder.State;

namespace Servitor.App.ArmyBuilder.Validation;

/// <summary>Checks an army list against the rules and names every rule it breaks. UI-independent.</summary>
public static class ArmyValidator
{
    public static IReadOnlyList<ArmyIssue> Validate(ArmySetup setup, ArmyTotals totals)
    {
        var issues = new List<ArmyIssue>();

        if (setup.BattleSize is { } battleSize && totals.Points > battleSize.PointsLimit)
        {
            issues.Add(new ArmyIssue(
                ArmyIssueKind.OverPointsBudget,
                $"The army costs {totals.Points} pts, {totals.Points - battleSize.PointsLimit} over the {battleSize.PointsLimit} pts limit of {battleSize.Name}."));
        }

        return issues;
    }
}
