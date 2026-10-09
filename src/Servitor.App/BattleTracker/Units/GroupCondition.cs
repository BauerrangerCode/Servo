namespace Servitor.App.BattleTracker.Units;

/// <summary>
/// The state of one group of identical models: the wounds the group has taken (<paramref name="Damage"/>), the status
/// tokens on it, and whether its wounds are shown counting down (remaining) or up (taken).
/// </summary>
public sealed record GroupCondition(int Damage = 0, int Tokens = 0, bool CountsDown = false)
{
    public static GroupCondition Fresh { get; } = new();
}
