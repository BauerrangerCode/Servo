namespace Servitor.App.Scoring.History;

/// <summary>Ends the running game: writes its scorecard to the history, then clears the setup and the scores for the next game.</summary>
public interface IGameRecorder
{
    /// <summary>Raised after a scorecard was saved and the game was cleared.</summary>
    event Action? Saved;

    /// <summary>The scorecard of the game that ended last in this session; null before the first one.</summary>
    Scorecard? LastEnded { get; }

    /// <summary>
    /// Returns the saved scorecard, or null when there is no game to end. If saving fails the exception propagates and
    /// the game stays untouched, so nothing is lost.
    /// </summary>
    Task<Scorecard?> EndGameAsync(CancellationToken cancellationToken = default);
}
