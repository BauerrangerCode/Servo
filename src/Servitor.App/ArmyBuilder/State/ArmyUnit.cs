using Servitor.RulesData.Domain;

namespace Servitor.App.ArmyBuilder.State;

/// <summary>
/// A unit in the army list: the datasheet, the size (model count and first-copy points) it was added with and the
/// wargear the player chose. The points the unit costs in the army come from <see cref="ArmyPricing"/>.
/// </summary>
public sealed record ArmyUnit(Guid Id, Datasheet Datasheet, UnitSize Size)
{
    public WargearSelection Wargear { get; init; } = WargearSelection.Default;
}
