namespace Servitor.App.ArmyBuilder.Validation;

public enum ArmyIssueKind
{
    OverPointsBudget,
}

/// <summary>One reason an army list is not valid: what kind of rule it breaks and a message saying so.</summary>
public sealed record ArmyIssue(ArmyIssueKind Kind, string Message);
