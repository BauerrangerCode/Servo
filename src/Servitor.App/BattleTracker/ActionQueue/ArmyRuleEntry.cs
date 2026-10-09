namespace Servitor.App.BattleTracker.ActionQueue;

/// <summary>
/// One entry of the Army Rules section: a detachment rule or a unit ability. <paramref name="Owner"/> is the detachment
/// or unit it belongs to. A null <paramref name="Timing"/> is a passive rule that is always in effect.
/// <paramref name="Options"/> is non-empty for a rule that makes the player pick one named alternative.
/// </summary>
public sealed record ArmyRuleEntry(
    string Id,
    string Title,
    string Owner,
    ArmyRuleSource Source,
    string Markdown,
    Timing? Timing,
    bool IsAura,
    IReadOnlyList<RuleOption> Options)
{
    public bool IsChoice => Options.Count > 0;
}
