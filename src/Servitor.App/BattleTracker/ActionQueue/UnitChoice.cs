using Servitor.App.ArmyBuilder.State;

namespace Servitor.App.BattleTracker.ActionQueue;

/// <summary>An army unit as offered for targeting; copies of the same datasheet are numbered so they can be told apart.</summary>
public sealed record UnitChoice(Guid Id, string Label)
{
    public static IReadOnlyList<UnitChoice> From(IReadOnlyList<ArmyUnit> units)
    {
        var copies = units.GroupBy(u => u.Datasheet.Name).ToDictionary(g => g.Key, g => g.Count());
        var seen = new Dictionary<string, int>();

        return [.. units.Select(unit =>
        {
            var name = unit.Datasheet.Name;
            seen[name] = seen.GetValueOrDefault(name) + 1;
            return new UnitChoice(unit.Id, copies[name] > 1 ? $"{name} ({seen[name]})" : name);
        })];
    }
}
