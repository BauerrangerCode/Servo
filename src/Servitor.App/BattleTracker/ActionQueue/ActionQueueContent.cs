using Servitor.RulesData.Domain;

namespace Servitor.App.BattleTracker.ActionQueue;

public sealed record ActionQueueContent(IReadOnlyList<Stratagem> CoreStratagems, IReadOnlyList<Stratagem> DetachmentStratagems)
{
    public static ActionQueueContent Empty { get; } = new([], []);
}
