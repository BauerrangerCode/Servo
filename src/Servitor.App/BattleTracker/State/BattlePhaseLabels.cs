namespace Servitor.App.BattleTracker.State;

public static class BattlePhaseLabels
{
    public static string Label(BattlePhase phase) => phase switch
    {
        BattlePhase.Deployment => "Deployment",
        BattlePhase.Command => "Command",
        BattlePhase.Movement => "Movement",
        BattlePhase.Shooting => "Shooting",
        BattlePhase.Charge => "Charge",
        BattlePhase.Fight => "Fight",
        BattlePhase.EndOfTurn => "End of Turn",
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };
}
