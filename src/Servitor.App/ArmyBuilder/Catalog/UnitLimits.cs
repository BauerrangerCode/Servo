using Servitor.App.ArmyBuilder.Rules;
using Servitor.RulesData.Domain;

namespace Servitor.App.ArmyBuilder.Catalog;

public static class UnitLimits
{
    /// <summary>
    /// How many copies of the datasheet one army may take, read from the datasheet's own constraint — its base value
    /// plus the Incursion override where the source gives one. Null when the data has no limit for it.
    /// </summary>
    public static int? MaxPerArmy(Datasheet datasheet, BattleSize battleSize)
    {
        if (datasheet.CompositionConstraint is not { } constraint)
        {
            return null;
        }

        return battleSize == BattleSizes.Incursion
            ? constraint.IncursionLimit ?? constraint.StrikeForceAndOnslaughtLimit
            : constraint.StrikeForceAndOnslaughtLimit;
    }

    /// <summary>
    /// The limit with the army's detachment rules applied: a rule that allows more copies of a unit ("up to three of
    /// each of the following models") raises the datasheet's own limit, never lowers it.
    /// </summary>
    public static int? MaxPerArmy(Datasheet datasheet, BattleSize battleSize, IEnumerable<DetachmentRule> activeRules, string? armyFaction)
    {
        var limit = MaxPerArmy(datasheet, battleSize);
        if (limit is null)
        {
            return null;
        }

        var raised = activeRules
            .OfType<UnitLimitOverrideRule>()
            .Where(r => r.Units.Matches(datasheet, armyFaction))
            .Select(r => r.Max);
        return raised.Prepend(limit.Value).Max();
    }
}
