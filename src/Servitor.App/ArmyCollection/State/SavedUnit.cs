using Servitor.App.ArmyBuilder.State;

namespace Servitor.App.ArmyCollection.State;

/// <summary>
/// One unit of a <see cref="SavedArmy"/>, referencing its datasheet by name rather than storing the catalog object
/// itself, so a saved army never forks or goes stale against later rules-data updates. <paramref name="Id"/> and
/// <paramref name="LeaderUnitId"/> are scoped to this saved army only — reopening it assigns each unit a fresh
/// <see cref="ArmyUnit.Id"/> and remaps leader references via these saved ids (see SavedArmyResolver).
/// </summary>
public sealed record SavedUnit(
    Guid Id,
    string DatasheetName,
    int ModelCount,
    WargearSelection Wargear,
    string? EnhancementName,
    string? AlliedFaction,
    bool IsWarlord,
    Guid? LeaderUnitId);
