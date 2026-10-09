using Servitor.App.ArmyBuilder.State;

namespace Servitor.App.BattleTracker.Units;

/// <summary>One datasheet inside a card: a unit, a leading character or a bodyguard unit, with its equipment and groups.</summary>
public sealed record UnitRole(string Label, ArmyUnit Unit, string UnitKey, UnitLoadout Loadout);

/// <summary>
/// What one datasheet card shows. A character leading a unit and the unit it leads form one card, since an attached
/// unit is one unit for all rules purposes; the roles tell the two apart.
/// </summary>
public sealed record UnitCardModel(string Title, IReadOnlyList<UnitRole> Roles)
{
    public IEnumerable<ModelGroup> Groups => Roles.SelectMany(r => r.Loadout.Groups);
}

public static class UnitCards
{
    public const string LeaderRole = "Leader";
    public const string BodyguardRole = "Bodyguard";
    public const string UnitRoleLabel = "Unit";

    public static IReadOnlyList<UnitCardModel> Build(IReadOnlyList<ArmyUnit> units)
    {
        var leaderIds = units.Where(u => u.LeaderUnitId is not null).Select(u => u.LeaderUnitId!.Value).ToHashSet();
        var cards = new List<UnitCardModel>();

        foreach (var unit in units.Where(u => !leaderIds.Contains(u.Id)))
        {
            var leader = unit.LeaderUnitId is { } id ? units.FirstOrDefault(u => u.Id == id) : null;
            if (leader is null)
            {
                cards.Add(new UnitCardModel(unit.Datasheet.Name, [Role(UnitRoleLabel, units, unit)]));
                continue;
            }

            cards.Add(new UnitCardModel(
                $"{unit.Datasheet.Name} + {leader.Datasheet.Name}",
                [Role(LeaderRole, units, leader), Role(BodyguardRole, units, unit)]));
        }

        return cards;
    }

    private static UnitRole Role(string label, IReadOnlyList<ArmyUnit> units, ArmyUnit unit) =>
        new(label, unit, UnitKeys.Of(units, unit), UnitLoadoutBuilder.Build(unit));
}
