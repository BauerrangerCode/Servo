using Servitor.App.BattleTracker.State;
using Servitor.App.Scoring.State;
using Servitor.App.Scoring.Tracking;

namespace Servitor.App.Scoring.History;

public sealed class GameRecorder(IBattleSetupState setup, IScoreTrackingState tracking, IBattleProgressState progress, IBattlePlaySession session, IScorecardStore store, TimeProvider time) : IGameRecorder
{
    public event Action? Saved;

    public Scorecard? LastEnded { get; private set; }

    public async Task<Scorecard?> EndGameAsync(CancellationToken cancellationToken = default)
    {
        if (ScorecardFactory.Create(setup, tracking, progress, time.GetUtcNow()) is not { } scorecard)
        {
            return null;
        }

        await store.SaveAsync(scorecard, cancellationToken);

        LastEnded = scorecard;
        tracking.Reset();
        setup.Reset();
        await session.ResetAsync();
        Saved?.Invoke();
        return scorecard;
    }
}
