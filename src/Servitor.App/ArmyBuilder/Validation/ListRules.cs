using Servitor.App.ArmyBuilder.Catalog;
using Servitor.App.ArmyBuilder.State;

namespace Servitor.App.ArmyBuilder.Validation;

/// <summary>The army may not cost more than the points total of the battle size.</summary>
public sealed class PointsBudgetRule : IArmyRule
{
    public IEnumerable<ArmyIssue> Check(ArmyContext context)
    {
        if (context.Setup.BattleSize is { } battleSize && context.Totals.Points > battleSize.PointsLimit)
        {
            yield return new ArmyIssue(
                ArmyIssueKind.OverPointsBudget,
                $"The army costs {context.Totals.Points} pts, {context.Totals.Points - battleSize.PointsLimit} over the {battleSize.PointsLimit} pts limit of {battleSize.Name}.");
        }
    }
}

/// <summary>The selected detachments may not cost more DP than the battle size gives.</summary>
public sealed class DetachmentPointsRule : IArmyRule
{
    public IEnumerable<ArmyIssue> Check(ArmyContext context)
    {
        if (context.Setup.BattleSize is { } battleSize && !DetachmentBudget.IsWithinBudget(battleSize, context.Setup.Detachments))
        {
            yield return new ArmyIssue(
                ArmyIssueKind.OverDetachmentPoints,
                $"The detachments cost {DetachmentBudget.Used(context.Setup.Detachments)} DP, over the {battleSize.DetachmentPointsLimit} DP of {battleSize.Name}.");
        }
    }
}

/// <summary>An army may not take more copies of one datasheet than the datasheet's own limit for the battle size allows.</summary>
public sealed class UnitLimitRule : IArmyRule
{
    public IEnumerable<ArmyIssue> Check(ArmyContext context)
    {
        if (context.Setup.BattleSize is not { } battleSize)
        {
            yield break;
        }

        foreach (var group in context.Units.GroupBy(u => (u.Datasheet.Name, u.Datasheet.Faction)))
        {
            var datasheet = group.First().Datasheet;
            if (UnitLimits.MaxPerArmy(datasheet, battleSize, context.ActiveRules, context.Setup.FactionId) is { } max && group.Count() > max)
            {
                yield return new ArmyIssue(
                    ArmyIssueKind.OverUnitLimit,
                    $"{datasheet.Name}: {group.Count()} units, but at most {max} are allowed in {battleSize.Name}.");
            }
        }
    }
}

/// <summary>A unit's model count has to be one of the sizes its datasheet offers.</summary>
public sealed class ModelCountRule : IArmyRule
{
    public IEnumerable<ArmyIssue> Check(ArmyContext context)
    {
        foreach (var unit in context.Units)
        {
            var sizes = unit.Datasheet.Sizes;
            if (unit.Size.Models > 0 && sizes.Count > 0 && sizes.All(s => s.Models != unit.Size.Models))
            {
                yield return new ArmyIssue(
                    ArmyIssueKind.InvalidModelCount,
                    $"{unit.Datasheet.Name} has {unit.Size.Models} models, but it can only be taken with {string.Join(" or ", sizes.Select(s => s.Models))}.");
            }
        }
    }
}

/// <summary>
/// A detachment that restricts the army to one Space Marines Chapter excludes the units of every other Chapter. A
/// unit's Chapter is the one of its catalogue; units of the general Space Marines catalogue fit any Chapter.
/// </summary>
public sealed class ChapterRestrictionRule : IArmyRule
{
    public IEnumerable<ArmyIssue> Check(ArmyContext context)
    {
        foreach (var detachment in context.Setup.Detachments.Where(d => d.RestrictedToChapter is not null))
        {
            var chapter = detachment.RestrictedToChapter!;
            var offending = context.Units
                .Select(u => (u.Datasheet.Name, Chapter: UnitChapters.Of(u.Datasheet)))
                .Where(x => x.Chapter is not null && !UnitChapters.Same(x.Chapter, chapter))
                .DistinctBy(x => x.Name);

            foreach (var (name, otherChapter) in offending)
            {
                yield return new ArmyIssue(
                    ArmyIssueKind.ChapterMix,
                    $"{detachment.Name} restricts the army to {chapter} units, but it includes {name} ({otherChapter}).");
            }
        }
    }
}
