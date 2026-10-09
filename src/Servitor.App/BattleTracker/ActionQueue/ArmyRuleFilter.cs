using Servitor.App.BattleTracker.State;

namespace Servitor.App.BattleTracker.ActionQueue;

/// <param name="Active">Rules to look at now: timed for this moment, or pinned, or with an active alternative.</param>
/// <param name="Passive">Rules that are always in effect and tied to no moment.</param>
public sealed record ArmyRuleView(IReadOnlyList<ArmyRuleEntry> Active, IReadOnlyList<ArmyRuleEntry> Passive);

public static class ArmyRuleFilter
{
    /// <summary>Aura rules are left out while auras are hidden, unless the player pinned them.</summary>
    public static ArmyRuleView Filter(IEnumerable<ArmyRuleEntry> entries, TurnContext context, IArmyRuleState state)
    {
        var active = new List<ArmyRuleEntry>();
        var passive = new List<ArmyRuleEntry>();

        foreach (var entry in entries)
        {
            var isPinned = state.IsPinned(entry.Id) || state.ActiveOption(entry.Id) is not null;
            if (entry.IsAura && !state.ShowAuras && !isPinned)
            {
                continue;
            }

            if (isPinned || entry.Timing?.IsActive(context) == true)
            {
                active.Add(entry);
            }
            else if (entry.Timing is null)
            {
                passive.Add(entry);
            }
        }

        return new ArmyRuleView(active, passive);
    }
}
