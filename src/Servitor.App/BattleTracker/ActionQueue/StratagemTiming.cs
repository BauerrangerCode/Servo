using System.Text.RegularExpressions;
using Servitor.App.BattleTracker.State;
using Servitor.RulesData.Domain;

namespace Servitor.App.BattleTracker.ActionQueue;

/// <summary>
/// Reads the free-text Phase, Turn and When fields of a stratagem (e.g. "Shooting or Fight phase", "Opponent's turn")
/// into a <see cref="Timing"/>. Text it does not recognise never hides a stratagem: unknown phase text means every
/// phase, and a When text without a known moment leaves the stratagem tied to its phase alone.
/// </summary>
public static class StratagemTiming
{
    private const string AnyPhase = "Any phase";
    private const string YourTurn = "Your";
    private const string OpponentTurn = "Opponent";

    private static readonly BattlePhase[] NamedPhases =
        [BattlePhase.Command, BattlePhase.Movement, BattlePhase.Shooting, BattlePhase.Charge, BattlePhase.Fight];

    private static readonly Regex TurnStartOrEnd = new(@"^(start|beginning) or end of (any|the|each) turn", RegexOptions.IgnoreCase);
    private static readonly Regex RoundStart = new(@"^(start|beginning) of (the |any |each )?(first |next )?battle round", RegexOptions.IgnoreCase);
    private static readonly Regex RoundEnd = new(@"^end of (the |any |each )?battle round", RegexOptions.IgnoreCase);
    private static readonly Regex TurnStart = new(@"^(start|beginning) of (your opponent.s |your |the |any |each )turn", RegexOptions.IgnoreCase);
    private static readonly Regex TurnEnd = new(@"^end of (your opponent.s |your |the |any |each )turn", RegexOptions.IgnoreCase);

    public static Timing Of(Stratagem stratagem)
    {
        var named = NamedPhases
            .Where(phase => stratagem.Phase.Contains(BattlePhaseLabels.Label(phase), StringComparison.OrdinalIgnoreCase))
            .ToHashSet();
        var anyPhase = named.Count == 0 || stratagem.Phase.Contains(AnyPhase, StringComparison.OrdinalIgnoreCase);

        return new Timing(
            ScopeOf(stratagem),
            anyPhase ? Enum.GetValues<BattlePhase>().ToHashSet() : named,
            anyPhase,
            anyPhase ? MomentOf(stratagem.When) : TimingMoment.None);
    }

    public static TurnScope ScopeOf(Stratagem stratagem) =>
        stratagem.Turn.StartsWith(YourTurn, StringComparison.OrdinalIgnoreCase) ? TurnScope.Your
        : stratagem.Turn.StartsWith(OpponentTurn, StringComparison.OrdinalIgnoreCase) ? TurnScope.Opponent
        : TurnScope.Either;

    /// <summary>Only a When text that opens with the moment counts; a mention further in ("until the start of your next turn") is an effect duration.</summary>
    private static TimingMoment MomentOf(string when)
    {
        var text = when.Trim();
        return TurnStartOrEnd.IsMatch(text) ? TimingMoment.TurnStartOrEnd
            : RoundStart.IsMatch(text) ? TimingMoment.RoundStart
            : RoundEnd.IsMatch(text) ? TimingMoment.RoundEnd
            : TurnStart.IsMatch(text) ? TimingMoment.TurnStart
            : TurnEnd.IsMatch(text) ? TimingMoment.TurnEnd
            : TimingMoment.None;
    }
}
