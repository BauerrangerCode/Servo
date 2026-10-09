namespace Servitor.App.ArmyBuilder.State;

/// <summary>
/// A weapon and the number of the unit's models that carry it, as an imported army list states it ("18x Slugga").
/// This is what a unit is really equipped with; Build's wargear selection only covers the options the datasheet offers.
/// </summary>
public sealed record DeclaredWeapon(string Name, int Count);
