namespace Servitor.App.BattleTracker.State;

/// <summary>Declaration order is the play order; Deployment happens once before the first battle round.</summary>
public enum BattlePhase
{
    Deployment,
    Command,
    Movement,
    Shooting,
    Charge,
    Fight,
    EndOfTurn,
}
