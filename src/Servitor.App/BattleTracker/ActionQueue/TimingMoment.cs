namespace Servitor.App.BattleTracker.ActionQueue;

/// <summary>A point in the game a rule is tied to instead of a whole phase: its first or last phase of a turn or of a battle round.</summary>
public enum TimingMoment
{
    None,
    RoundStart,
    RoundEnd,
    TurnStart,
    TurnEnd,
    TurnStartOrEnd,
}
