namespace Servitor.App.ArmyBuilder.Validation;

/// <summary>The rule an <see cref="ArmyIssue"/> breaks; each rule type has its own kind so violations are told apart.</summary>
public enum ArmyIssueKind
{
    OverPointsBudget,
    OverDetachmentPoints,
    OverUnitLimit,
    InvalidModelCount,
    ChapterMix,
    EnhancementNotAllowed,
    DuplicateEnhancement,
    TooManyUpgradeCopies,
    TooManyEnhancements,
    EnhancementDetachmentNotSelected,
    MissingRequiredUnit,
    ForbiddenUnit,
    OverDetachmentPointsCap,
    AllyNotAllowed,
}

/// <summary>One reason an army list is not valid: what kind of rule it breaks and a message saying so.</summary>
public sealed record ArmyIssue(ArmyIssueKind Kind, string Message);
