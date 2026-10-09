using Servitor.App.BattleTracker.State;
using Servitor.RulesData.Domain;

namespace Servitor.App.BattleTracker.ActionQueue;

/// <summary>Reads the free-text Phase and Turn fields of a stratagem (e.g. "Shooting or Fight phase", "Opponent's turn").</summary>
public static class StratagemTiming
{
    private const string AnyPhase = "Any phase";
    private const string YourTurn = "Your";
    private const string OpponentTurn = "Opponent";

    private static readonly BattlePhase[] NamedPhases =
        [BattlePhase.Command, BattlePhase.Movement, BattlePhase.Shooting, BattlePhase.Charge, BattlePhase.Fight];

    /// <summary>
    /// The phases a stratagem belongs to. "Any phase", and text this class does not recognise, mean every phase, so a
    /// stratagem is never hidden because of wording it does not understand.
    /// </summary>
    public static IReadOnlySet<BattlePhase> PhasesOf(Stratagem stratagem)
    {
        var named = NamedPhases
            .Where(phase => stratagem.Phase.Contains(BattlePhaseLabels.Label(phase), StringComparison.OrdinalIgnoreCase))
            .ToHashSet();

        return named.Count == 0 || stratagem.Phase.Contains(AnyPhase, StringComparison.OrdinalIgnoreCase)
            ? Enum.GetValues<BattlePhase>().ToHashSet()
            : named;
    }

    public static TurnScope ScopeOf(Stratagem stratagem) =>
        stratagem.Turn.StartsWith(YourTurn, StringComparison.OrdinalIgnoreCase) ? TurnScope.Your
        : stratagem.Turn.StartsWith(OpponentTurn, StringComparison.OrdinalIgnoreCase) ? TurnScope.Opponent
        : TurnScope.Either;
}
