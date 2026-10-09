namespace Servitor.App.BattleTracker.Units;

/// <summary>The state of one group of identical models: the wounds the group has taken (<paramref name="Damage"/>) and the status tokens on it.</summary>
public sealed record GroupCondition(int Damage = 0, int Tokens = 0)
{
    public static GroupCondition Fresh { get; } = new();
}
