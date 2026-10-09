using Servitor.App.ArmyBuilder.State;

namespace Servitor.App.BattleTracker.Units;

/// <summary>
/// A key for a unit that stays the same when the army is restored after a reload (unit ids are new each time): the
/// datasheet name and which copy of it the unit is, in army order.
/// </summary>
public static class UnitKeys
{
    public static string Of(IReadOnlyList<ArmyUnit> units, ArmyUnit unit)
    {
        var copy = units.TakeWhile(u => u.Id != unit.Id).Count(u => u.Datasheet.Name == unit.Datasheet.Name) + 1;
        return $"{unit.Datasheet.Name}#{copy}";
    }

    public static string GroupKey(string unitKey, ModelGroup group) => $"{unitKey}|{group.Key}";
}
