namespace Servitor.App.BattleTracker.State;

/// <summary>Everything Play tracks for one game, so that a finished game can be wiped in one step.</summary>
public interface IBattlePlaySession
{
    /// <summary>Resets the progress, Command Points, stratagem uses, rule pins and the opponent's army.</summary>
    Task ResetAsync();
}
