using Servitor.App.ArmyBuilder.State;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.BattleTracker.ActionQueue;

public static class ActionQueueBuilder
{
    /// <summary>
    /// What is relevant right now (phase and whose turn it is): the Core Stratagems, plus the stratagems of the detachments the army
    /// took. Only standard-game stratagems are listed; Boarding Actions and Challenger have their own.
    /// </summary>
    public static ActionQueueContent Build(MergedRulesData? data, ArmySetup setup, TurnContext context)
    {
        if (data is null)
        {
            return ActionQueueContent.Empty;
        }

        return new ActionQueueContent(
            ForTurn(data.CoreStratagems, context),
            ForTurn(DetachmentStratagems(data, setup), context));
    }

    /// <summary>The standard-game stratagems of the army, whatever the phase and turn: the unfiltered Commands reference.</summary>
    public static ActionQueueContent BuildAll(MergedRulesData? data, ArmySetup setup) =>
        data is null
            ? ActionQueueContent.Empty
            : new ActionQueueContent(
                [.. data.CoreStratagems.Where(IsStandard)],
                [.. DetachmentStratagems(data, setup).Where(IsStandard)]);

    private static bool IsStandard(Stratagem stratagem) => stratagem.Mode == GameMode.Standard;

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

    private static IReadOnlyList<Stratagem> ForTurn(IEnumerable<Stratagem> stratagems, TurnContext context) =>
        stratagems
            .Where(s => IsStandard(s) && StratagemTiming.Of(s).IsActive(context))
            .ToList();
}
