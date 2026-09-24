namespace Servitor.RulesData.Domain;

/// <summary>
/// The three official Battle Sizes. BSData's game-system file encodes these only as conditional modifiers on its
/// Army Roster entry (see docs/Reference-App-Analysis.md Section 12), which cannot be read as plain fields, so the
/// confirmed values are kept here in one place. Update them together with the BSData source version.
/// </summary>
public static class BattleSizes
{
    public static readonly BattleSize Incursion = new("Incursion", 1000, 2, 2);
    public static readonly BattleSize StrikeForce = new("Strike Force", 2000, 3, 4);
    public static readonly BattleSize Onslaught = new("Onslaught", 3000, 4, 4);

    public static IReadOnlyList<BattleSize> All { get; } = [Incursion, StrikeForce, Onslaught];
}
