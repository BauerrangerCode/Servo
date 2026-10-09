using Servitor.App.BattleTracker.State;

namespace Servitor.App.Tests.BattleTracker;

internal sealed class FakeBattlePlaySession : IBattlePlaySession
{
    public int ResetCount { get; private set; }

    public Task ResetAsync()
    {
        ResetCount++;
        return Task.CompletedTask;
    }
}
