using Servitor.App.ArmyBuilder.State;
using Servitor.App.BattleTracker.State;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.BattleTracker.ActionQueue;

public static class ActionQueueBuilder
{
    /// <summary>
    /// What is relevant in the given phase: the Core Stratagems, plus the stratagems of the detachments the army
    /// took. Only standard-game stratagems are listed; Boarding Actions and Challenger have their own.
    /// </summary>
    public static ActionQueueContent Build(MergedRulesData? data, ArmySetup setup, BattlePhase phase)
    {
        if (data is null)
        {
            return ActionQueueContent.Empty;
        }

        return new ActionQueueContent(
            ForPhase(data.CoreStratagems, phase),
            ForPhase(DetachmentStratagems(data, setup), phase));
    }

    private static IEnumerable<Stratagem> DetachmentStratagems(MergedRulesData data, ArmySetup setup)
    {
        var faction = data.Factions.FirstOrDefault(f => f.FactionId == setup.FactionId);
        if (faction is null)
        {
            return [];
        }

        var detachmentNames = setup.Detachments.Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return faction.Stratagems.Where(s => detachmentNames.Contains(s.Detachment));
    }

    private static IReadOnlyList<Stratagem> ForPhase(IEnumerable<Stratagem> stratagems, BattlePhase phase) =>
        stratagems
            .Where(s => s.Mode == GameMode.Standard && StratagemTiming.PhasesOf(s).Contains(phase))
            .ToList();
}
