namespace Servitor.App.BattleTracker.State;

/// <summary>
/// The persisted choices around the Army Rules: whether auras are listed, which rules the player pinned (with an
/// optional note such as the chosen target) and, for rules with named alternatives, the active and the already used ones.
/// </summary>
public sealed record ArmyRuleSettings(
    bool ShowAuras,
    IReadOnlyDictionary<string, string> Pins,
    IReadOnlyDictionary<string, string> ActiveOptions,
    IReadOnlyDictionary<string, IReadOnlyList<string>> UsedOptions)
{
    public static ArmyRuleSettings Default { get; } = new(true, new Dictionary<string, string>(), new Dictionary<string, string>(), new Dictionary<string, IReadOnlyList<string>>());
}
