using Servitor.RulesData.Domain;

namespace Servitor.App.ArmyBuilder.State;

/// <summary>
/// A unit in the army list: the datasheet, the size (model count and first-copy points) it was added with and the
/// wargear the player chose. The points the unit costs in the army come from <see cref="ArmyPricing"/>.
/// </summary>
public sealed record ArmyUnit(Guid Id, Datasheet Datasheet, UnitSize Size)
{
    public WargearSelection Wargear { get; init; } = WargearSelection.Default;

    /// <summary>The weapons an imported list says the unit carries; null for a unit built by hand, where only <see cref="Wargear"/> is known.</summary>
    public IReadOnlyList<DeclaredWeapon>? DeclaredWeapons { get; init; }

    /// <summary>The enhancement the unit carries, or null. Its cost is added to the unit's points.</summary>
    public Enhancement? Enhancement { get; init; }

    /// <summary>The faction (rules-text slug) the unit was taken from as an ally, or null for a unit of the army's own faction.</summary>
    public string? AlliedFaction { get; init; }

    /// <summary>Whether this unit's model is the army's Warlord. At most one unit in the army has this set.</summary>
    public bool IsWarlord { get; init; }

    /// <summary>
    /// The <see cref="Id"/> of the character <see cref="ArmyUnit"/> leading this unit, or null when unattached.
    /// Only set on the led (non-character) unit — a leader's own led units are derived by filtering on this field,
    /// not stored on the leader itself, so the two sides can never drift out of sync.
    /// </summary>
    public Guid? LeaderUnitId { get; init; }
}
