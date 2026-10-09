using Servitor.App.RulesData.Storage;
using Servitor.App.Scoring.Tracking;
using Servitor.RulesData.Domain;

namespace Servitor.App.BattleTracker.State;

public sealed class StratagemUsageState(IRulesDataStore store, IBattleProgressState progress) : IStratagemUsageState
{
    private const string Key = "battle/stratagem-uses";

    private bool _isLoaded;
    private List<StratagemUse> _uses = [];

    public event Action? Changed;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;
        if (await store.LoadAsync<List<StratagemUse>>(Key, cancellationToken) is { } stored)
        {
            _uses = stored;
            Changed?.Invoke();
        }
    }

    public bool IsUsedThisPhase(string stratagemId) => ThisPhase().Any(u => u.StratagemId == stratagemId);

    public StratagemUse? TargetingThisPhase(Guid unitId) => ThisPhase().FirstOrDefault(u => u.TargetUnitId == unitId);

    public async Task<StratagemUseOutcome> UseAsync(Stratagem stratagem, Guid? targetUnitId, bool allowRetarget = false)
    {
        if (IsUsedThisPhase(stratagem.Id))
        {
            return new StratagemUseOutcome(StratagemUseResult.AlreadyUsedThisPhase);
        }

        if (!allowRetarget && targetUnitId is { } unitId && TargetingThisPhase(unitId) is { } conflict)
        {
            return new StratagemUseOutcome(StratagemUseResult.TargetAlreadyTargeted, conflict.StratagemName);
        }

        var cost = stratagem.CpCost ?? 0;
        if (!await progress.SpendCommandPointsAsync(cost))
        {
            return new StratagemUseOutcome(StratagemUseResult.NotEnoughCommandPoints);
        }

        _uses.Add(new StratagemUse(progress.Round, progress.ActiveTurn, progress.Phase, stratagem.Id, stratagem.Name, cost, targetUnitId));
        await SaveAsync();
        return new StratagemUseOutcome(StratagemUseResult.Used);
    }

    public async Task UndoAsync(string stratagemId)
    {
        if (ThisPhase().FirstOrDefault(u => u.StratagemId == stratagemId) is not { } use)
        {
            return;
        }

        _uses.Remove(use);
        await progress.AdjustCommandPointsAsync(Side.You, use.CpCost);
        await SaveAsync();
    }

    private IEnumerable<StratagemUse> ThisPhase() =>
        _uses.Where(u => u.Round == progress.Round && u.Turn == progress.ActiveTurn && u.Phase == progress.Phase);

    private async Task SaveAsync()
    {
        Changed?.Invoke();
        await store.SaveAsync(Key, _uses.ToList());
    }
}
