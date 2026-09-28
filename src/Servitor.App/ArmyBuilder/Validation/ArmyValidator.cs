using Servitor.App.ArmyBuilder.Rules;
using Servitor.App.ArmyBuilder.State;

namespace Servitor.App.ArmyBuilder.Validation;

/// <summary>Checks an army list against every composition rule and names each rule it breaks. UI-independent.</summary>
public static class ArmyValidator
{
    private static readonly IArmyRule[] Rules =
    [
        new PointsBudgetRule(),
        new DetachmentPointsRule(),
        new UnitLimitRule(),
        new MustIncludeCheck(),
        new CannotIncludeCheck(),
        new PointsCapCheck(),
        new ModelCountRule(),
        new ChapterRestrictionRule(),
        new AllyRule(),
        new EnhancementEligibilityRule(),
        new EnhancementUniquenessRule(),
        new EnhancementLimitRule(),
        new EnhancementDetachmentRule(),
    ];

    public static IReadOnlyList<ArmyIssue> Validate(ArmySetup setup, IReadOnlyList<ArmyUnit> units, DetachmentRuleSet? rules = null) =>
        Validate(ArmyContext.Create(setup, units, rules));

    public static IReadOnlyList<ArmyIssue> Validate(ArmyContext context) =>
        Rules.SelectMany(rule => rule.Check(context)).ToList();
}
