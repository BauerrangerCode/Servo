using Servitor.RulesData.Domain;

namespace Servitor.App.BattleTracker.State;

/// <summary>
/// Which stratagems the player used (E6.2b). Enforces the Core Rules' two limits per phase: the same stratagem cannot be
/// used twice, and a unit cannot be the target of more than one stratagem. Using one also pays its Command Points.
/// Persisted on every change, like the battle progress.
/// </summary>
public interface IStratagemUsageState
{
    event Action? Changed;

    Task LoadAsync(CancellationToken cancellationToken = default);

    bool IsUsedThisPhase(string stratagemId);

    /// <summary>The stratagem that already targeted the unit in the phase being played, or null.</summary>
    StratagemUse? TargetingThisPhase(Guid unitId);

    /// <summary>
    /// Uses the stratagem if the limits and the Command Points allow it; a refused use changes nothing.
    /// </summary>
    /// <param name="targetUnitId">The army unit the stratagem targets; null when it targets no unit of the army.</param>
    /// <param name="allowRetarget">
    /// Lets a unit be targeted again. Some stratagems state they may be used on a unit that was already targeted; the
    /// player reads that on the card and overrides the block.
    /// </param>
    Task<StratagemUseOutcome> UseAsync(Stratagem stratagem, Guid? targetUnitId, bool allowRetarget = false);

    /// <summary>Forgets every use, for a new game.</summary>
    Task ResetAsync();

    /// <summary>Takes back a use of the current phase and refunds its Command Points.</summary>
    Task UndoAsync(string stratagemId);
}
