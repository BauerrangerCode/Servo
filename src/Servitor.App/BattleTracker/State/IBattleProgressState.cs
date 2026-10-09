using Servitor.App.Scoring.Tracking;

namespace Servitor.App.BattleTracker.State;

/// <summary>
/// Where the current game is: battle round, whose turn it is and the phase (E6.1). Lives in a scoped service so the
/// Action Queue (E6.2) and the wound tracking (E6.6) can follow it, and is persisted on every change so a reload
/// mid-game loses nothing. Both players' Command Points are part of it, since they are credited
/// when a Command phase begins. A round holds two turns, the first player's and then the other's; the round counter
/// advances when the second turn ends. Deployment happens once before the first turn.
/// </summary>
public interface IBattleProgressState
{
    event Action? Changed;

    int Round { get; }

    BattlePhase Phase { get; }

    /// <summary>Who takes the first turn of every round.</summary>
    Side FirstTurn { get; }

    /// <summary>Whose turn is being played.</summary>
    Side ActiveTurn { get; }

    int CommandPointsOf(Side side);

    /// <summary>Restores the persisted progress; does nothing after the first call.</summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deployment → Command → … → End of Turn → the other player's Command; after the second turn of a round, the next
    /// round's first-turn Command. Does nothing after the last turn of the last round.
    /// </summary>
    Task NextPhaseAsync();

    /// <summary>The reverse of <see cref="NextPhaseAsync"/>; the first Command phase leads back to Deployment.</summary>
    Task PreviousPhaseAsync();

    /// <summary>Jumps to a phase of the current turn, e.g. back to Deployment.</summary>
    Task SetPhaseAsync(BattlePhase phase);

    /// <summary>Picks who takes the first turn; only possible during Deployment.</summary>
    Task SetFirstTurnAsync(Side side);

    Task IncrementRoundAsync();

    Task DecrementRoundAsync();

    /// <summary>Changes a balance by hand; it never drops below zero.</summary>
    Task AdjustCommandPointsAsync(Side side, int delta);

    /// <summary>Pays a stratagem's cost from the player's balance; false, changing nothing, when the balance is too low.</summary>
    Task<bool> SpendCommandPointsAsync(int cost);
}
