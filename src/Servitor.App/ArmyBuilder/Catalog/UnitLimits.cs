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
}
