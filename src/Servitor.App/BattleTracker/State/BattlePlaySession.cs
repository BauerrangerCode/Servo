using Servitor.App.BattleTracker.Opponent;

namespace Servitor.App.BattleTracker.State;

public sealed class BattlePlaySession(
    IBattleProgressState progress,
    IStratagemUsageState usage,
    IArmyRuleState rules,
    IOpponentArmyState opponent) : IBattlePlaySession
{
    public async Task ResetAsync()
    {
        await progress.ResetAsync();
        await usage.ResetAsync();
        await rules.ResetAsync();
        opponent.Clear();
    }
}
