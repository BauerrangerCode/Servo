using Servitor.RulesData.Domain;

namespace Servitor.App.BattleTracker.Units;

/// <summary>
/// Models of a unit that carry the same weapons and so are interchangeable in play. <paramref name="Key"/> is stable
/// for a unit's loadout and names the group in the saved wounds.
/// </summary>
public sealed record ModelGroup(string Key, string Label, int Models, int WoundsPerModel, IReadOnlyList<string> WeaponNames);

/// <summary>
/// A weapon the unit is equipped with, the number of models that carry it, and the datasheet profiles that describe it
/// (empty when the datasheet has no profile of that name).
/// </summary>
public sealed record EquippedWeapon(string Name, int Models, IReadOnlyList<WeaponProfile> Profiles);

public sealed record UnitLoadout(IReadOnlyList<ModelGroup> Groups, IReadOnlyList<EquippedWeapon> Weapons)
{
    public int Models => Groups.Sum(g => g.Models);

    public IEnumerable<ModelGroup> GroupsWith(EquippedWeapon weapon) =>
        Groups.Where(g => g.WeaponNames.Contains(weapon.Name, StringComparer.Ordinal));
}
