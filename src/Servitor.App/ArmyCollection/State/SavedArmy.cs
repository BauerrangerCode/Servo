namespace Servitor.App.ArmyCollection.State;

/// <summary>
/// A snapshot of a built or imported army as stored in My Collection (E4.3): lightweight references (faction,
/// detachment and unit names, a battle size name) rather than the catalog objects themselves, resolved back against
/// the current rules data when reopened (see SavedArmyResolver). Keeping only references means a rules-data update
/// is picked up automatically and a saved army never duplicates or forks rules content.
/// </summary>
public sealed record SavedArmy(
    Guid Id,
    string Name,
    DateTimeOffset SavedAtUtc,
    string FactionId,
    string BattleSizeName,
    IReadOnlyList<string> AllyFactionIds,
    IReadOnlyList<string> DetachmentNames,
    IReadOnlyList<SavedUnit> Units);
