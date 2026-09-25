using Servitor.App.ArmyBuilder.Catalog;
using Servitor.RulesData.Domain;

namespace Servitor.App.ArmyBuilder.State;

/// <summary>Which units an army may still take. UI-independent so it is testable on its own.</summary>
public static class UnitRules
{
    /// <summary>How many copies of the datasheet are in the list. A datasheet is identified by name and catalogue.</summary>
    public static int CountOf(IEnumerable<ArmyUnit> units, Datasheet datasheet) =>
        units.Count(u => u.Datasheet.Name == datasheet.Name && u.Datasheet.Faction == datasheet.Faction);

    public static AddUnitResult CanAdd(ArmySetup setup, IReadOnlyList<ArmyUnit> units, Datasheet datasheet)
    {
        if (setup.BattleSize is not { } battleSize)
        {
            return AddUnitResult.NoBattleSize;
        }

        if (setup.FactionId is not { } factionId)
        {
            return AddUnitResult.NoFaction;
        }

        if (!datasheet.FactionSlugs.Contains(factionId))
        {
            return AddUnitResult.NotInFaction;
        }

        return UnitLimits.MaxPerArmy(datasheet, battleSize) is { } max && CountOf(units, datasheet) >= max
            ? AddUnitResult.AtLimit
            : AddUnitResult.Added;
    }
}
