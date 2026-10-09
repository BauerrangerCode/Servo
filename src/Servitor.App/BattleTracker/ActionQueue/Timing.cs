using Servitor.App.BattleTracker.State;
using Servitor.App.Scoring.Tracking;

namespace Servitor.App.BattleTracker.ActionQueue;

/// <summary>
/// When a rule can be used: in which player's turn, in which phases, or at a <see cref="TimingMoment"/>.
/// <paramref name="AnyPhase"/> says the rule is not tied to a phase, i.e. <paramref name="Phases"/> is every phase.
/// </summary>
public sealed record Timing(TurnScope Scope, IReadOnlySet<BattlePhase> Phases, bool AnyPhase, TimingMoment Moment)
{
    public bool IsActive(TurnContext context)
    {
        // Deployment belongs to no player's turn, so only rules without a turn or moment tie apply there.
        if (context.Phase == BattlePhase.Deployment)
        {
            return Moment == TimingMoment.None && Phases.Contains(context.Phase);
        }

        if (!ScopeMatches(context.ActiveTurn))
        {
            return false;
        }

        var isFirstTurn = context.ActiveTurn == context.FirstTurn;
        return Moment switch
        {
            TimingMoment.None => Phases.Contains(context.Phase),
            TimingMoment.TurnStart => context.Phase == BattlePhase.Command,
            TimingMoment.TurnEnd => context.Phase == BattlePhase.EndOfTurn,
            TimingMoment.TurnStartOrEnd => context.Phase is BattlePhase.Command or BattlePhase.EndOfTurn,
            TimingMoment.RoundStart => context.Phase == BattlePhase.Command && isFirstTurn,
            TimingMoment.RoundEnd => context.Phase == BattlePhase.EndOfTurn && !isFirstTurn,
            _ => throw new ArgumentOutOfRangeException(nameof(context), Moment, null),
        };
    }

    private bool ScopeMatches(Side activeTurn) => Scope switch
    {
        TurnScope.Your => activeTurn == Side.You,
        TurnScope.Opponent => activeTurn == Side.Enemy,
        _ => true,
    };
}
