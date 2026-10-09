using Servitor.App.Scoring.Tracking;

namespace Servitor.App.BattleTracker.State;

/// <summary>The turn whose Command phase already granted its Command Points.</summary>
public sealed record CommandPointGrant(int Round, Side Turn);

/// <summary>
/// Both players' Command Points. Both players gain <see cref="PointsPerCommandPhase"/> at the start of every
/// Command phase, the opponent's included; <paramref name="LastGrant"/> keeps stepping back and forward over the same
/// Command phase from paying out twice.
/// </summary>
public sealed record CommandPointLedger(int You, int Enemy, CommandPointGrant? LastGrant = null)
{
    public const int PointsPerCommandPhase = 1;

    public static CommandPointLedger Empty { get; } = new(0, 0);

    public bool IsValid => You >= 0 && Enemy >= 0;

    public int Of(Side side) => side == Side.You ? You : Enemy;

    public CommandPointLedger GrantForCommandPhase(int round, Side turn) =>
        LastGrant == new CommandPointGrant(round, turn)
            ? this
            : new CommandPointLedger(You + PointsPerCommandPhase, Enemy + PointsPerCommandPhase, new CommandPointGrant(round, turn));

    /// <summary>Changes a balance by hand (starting points, effects that grant or refund CP); never below zero.</summary>
    public CommandPointLedger Adjust(Side side, int delta) =>
        side == Side.You ? this with { You = Math.Max(0, You + delta) } : this with { Enemy = Math.Max(0, Enemy + delta) };

    public bool CanSpend(Side side, int cost) => Of(side) >= cost;

    public CommandPointLedger Spend(Side side, int cost) => Adjust(side, -cost);
}
