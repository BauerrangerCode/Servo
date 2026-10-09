using Servitor.App.BattleTracker.Opponent;

namespace Servitor.App.BattleTracker.State;

public sealed class BattlePlaySession(
    IBattleProgressState progress,
    IStratagemUsageState usage,
    IArmyRuleState rules,
    IOpponentArmyState opponent,
    IUnitConditionState conditions) : IBattlePlaySession
{
    public async Task ResetAsync()
    {
        await progress.ResetAsync();
        await usage.ResetAsync();
        await rules.ResetAsync();
        await conditions.ResetAsync();
        opponent.Clear();
    }
}
