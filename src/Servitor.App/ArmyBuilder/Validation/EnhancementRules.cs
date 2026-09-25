using Servitor.App.ArmyBuilder.Catalog;
using Servitor.App.ArmyBuilder.State;

namespace Servitor.App.ArmyBuilder.Validation;

/// <summary>
/// Who can carry an enhancement: only Character units, and never Epic Heroes. An enhancement tagged Upgrade may also go
/// on units that are not Characters.
/// </summary>
public sealed class EnhancementEligibilityRule : IArmyRule
{
    public IEnumerable<ArmyIssue> Check(ArmyContext context)
    {
        foreach (var unit in context.Units.Where(u => u.Enhancement is not null))
        {
            var enhancement = unit.Enhancement!;
            var role = UnitRoles.Of(unit.Datasheet);
            var eligible = role != UnitRole.EpicHero && (enhancement.IsUpgrade || role == UnitRole.Character);
            if (!eligible)
            {
                var who = role == UnitRole.EpicHero ? "Epic Heroes cannot take enhancements" : "only Character units can take an enhancement that is not an Upgrade";
                yield return new ArmyIssue(
                    ArmyIssueKind.EnhancementNotAllowed,
                    $"{enhancement.Name} cannot be given to {unit.Datasheet.Name}: {who}.");
            }
        }
    }
}

/// <summary>An ordinary enhancement can be taken once per army; an Upgrade up to three times.</summary>
public sealed class EnhancementUniquenessRule : IArmyRule
{
    private const int MaxUpgradeCopies = 3;

    public IEnumerable<ArmyIssue> Check(ArmyContext context)
    {
        foreach (var group in context.Units.Where(u => u.Enhancement is not null).GroupBy(u => u.Enhancement!.Id))
        {
            var enhancement = group.First().Enhancement!;
            var copies = group.Count();
            if (enhancement.IsUpgrade && copies > MaxUpgradeCopies)
            {
                yield return new ArmyIssue(
                    ArmyIssueKind.TooManyUpgradeCopies,
                    $"The Upgrade {enhancement.Name} is taken {copies} times; an army can include at most {MaxUpgradeCopies}.");
            }
            else if (!enhancement.IsUpgrade && copies > 1)
            {
                yield return new ArmyIssue(
                    ArmyIssueKind.DuplicateEnhancement,
                    $"{enhancement.Name} is taken {copies} times; an army can include an enhancement only once.");
            }
        }
    }
}

/// <summary>
/// The army may carry only as many enhancements as its battle size allows. Only the first copy of an Upgrade counts;
/// further copies of it cost points but do not use up the limit.
/// </summary>
public sealed class EnhancementLimitRule : IArmyRule
{
    public IEnumerable<ArmyIssue> Check(ArmyContext context)
    {
        if (context.Setup.BattleSize is not { } battleSize)
        {
            yield break;
        }

        var counted = context.Units
            .Where(u => u.Enhancement is not null)
            .Select(u => u.Enhancement!)
            .Where(e => !e.IsUpgrade)
            .Count()
            + context.Units.Where(u => u.Enhancement is { IsUpgrade: true }).Select(u => u.Enhancement!.Id).Distinct().Count();

        if (counted > battleSize.EnhancementLimit)
        {
            yield return new ArmyIssue(
                ArmyIssueKind.TooManyEnhancements,
                $"The army has {counted} enhancements, over the limit of {battleSize.EnhancementLimit} of {battleSize.Name}.");
        }
    }
}

/// <summary>Enhancements come from the detachments the army selected.</summary>
public sealed class EnhancementDetachmentRule : IArmyRule
{
    public IEnumerable<ArmyIssue> Check(ArmyContext context)
    {
        var selected = context.Setup.Detachments.Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var unit in context.Units.Where(u => u.Enhancement is not null && !selected.Contains(u.Enhancement.Detachment)))
        {
            yield return new ArmyIssue(
                ArmyIssueKind.EnhancementDetachmentNotSelected,
                $"{unit.Enhancement!.Name} on {unit.Datasheet.Name} belongs to the detachment {unit.Enhancement.Detachment}, which the army does not include.");
        }
    }
}
