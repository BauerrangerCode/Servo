using Servitor.App.Scoring.State;
using Servitor.App.Scoring.Tracking;

namespace Servitor.App.Scoring.History;

public static class ScorecardFactory
{
    /// <summary>Null when the setup has no pair of dispositions, i.e. there is no game to record.</summary>
    public static Scorecard? Create(IBattleSetupState setup, IScoreTrackingState tracking, DateTimeOffset endedAtUtc)
    {
        if (setup.YourDisposition is not { } yours || setup.OpponentDisposition is not { } opponent)
        {
            return null;
        }

        var you = SideOf(tracking, Side.You);
        var enemy = SideOf(tracking, Side.Enemy);
        return new Scorecard(
            Guid.NewGuid(),
            endedAtUtc,
            yours,
            opponent,
            setup.ResolvedMission?.Name ?? "",
            setup.OpponentMission?.Name ?? "",
            setup.SelectedDeploymentCard?.Name,
            setup.Role,
            setup.FirstTurn,
            you,
            enemy,
            ResultOf(you.Total, enemy.Total));
    }

    public static GameResult ResultOf(int you, int enemy) =>
        you == enemy ? GameResult.Draw : you > enemy ? GameResult.Victory : GameResult.Defeat;

    private static SideScorecard SideOf(IScoreTrackingState tracking, Side side)
    {
        var score = tracking.ScoreOf(side);
        return new SideScorecard(
            [.. score.PrimaryRounds],
            score.PrimaryEndOfBattle,
            score.Primary,
            tracking.ModeOf(side),
            [.. score.SecondaryRounds],
            score.Secondary,
            score.Paint,
            score.Challenger,
            score.Total);
    }
}
