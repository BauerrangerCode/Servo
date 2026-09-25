using Servitor.App.ArmyBuilder.Rules;

namespace Servitor.App.ArmyBuilder.Validation;

// The checks of the rules a detachment adds (see DetachmentRule). Each rule type has its own check; which rules are in
// force is data (wwwroot/rules/detachment-rules.json), the checks only say what each type means.

/// <summary>The army has to include at least the number of units a "mustInclude" rule asks for.</summary>
public sealed class MustIncludeCheck : IArmyRule
{
    public IEnumerable<ArmyIssue> Check(ArmyContext context)
    {
        foreach (var rule in context.ActiveRules.OfType<MustIncludeRule>())
        {
            var count = context.Units.Count(u => rule.Units.Matches(u.Datasheet, context.Setup.FactionId));
            if (count < rule.Min)
            {
                yield return new ArmyIssue(
                    ArmyIssueKind.MissingRequiredUnit,
                    $"{rule.Detachment} needs at least {rule.Min} {rule.Label} in the army, but it has {count}.");
            }
        }
    }
}

/// <summary>The army may not include any of the units a "cannotInclude" rule names; each such datasheet is reported once.</summary>
public sealed class CannotIncludeCheck : IArmyRule
{
    public IEnumerable<ArmyIssue> Check(ArmyContext context)
    {
        foreach (var rule in context.ActiveRules.OfType<CannotIncludeRule>())
        {
            var forbidden = context.Units
                .Select(u => u.Datasheet)
                .Where(d => rule.Units.Matches(d, context.Setup.FactionId))
                .Select(d => d.Name)
                .Distinct();

            foreach (var name in forbidden)
            {
                yield return new ArmyIssue(
                    ArmyIssueKind.ForbiddenUnit,
                    $"{rule.Detachment} does not allow {rule.Label} in the army, but it includes {name}.");
            }
        }
    }
}

/// <summary>The units a "pointsCap" rule is about may together cost no more than the cap for the battle size.</summary>
public sealed class PointsCapCheck : IArmyRule
{
    public IEnumerable<ArmyIssue> Check(ArmyContext context)
    {
        if (context.Setup.BattleSize is not { } battleSize)
        {
            yield break;
        }

        foreach (var rule in context.ActiveRules.OfType<PointsCapRule>())
        {
            if (!rule.CapsByBattleSize.TryGetValue(battleSize.Name, out var cap))
            {
                continue;
            }

            var points = context.Priced
                .Where(p => rule.Units.Matches(p.Unit.Datasheet, context.Setup.FactionId))
                .Sum(p => p.Points);
            if (points > cap)
            {
                yield return new ArmyIssue(
                    ArmyIssueKind.OverDetachmentPointsCap,
                    $"{rule.Detachment} allows {rule.Label} up to {cap} pts in {battleSize.Name}, but they cost {points} pts.");
            }
        }
    }
}
