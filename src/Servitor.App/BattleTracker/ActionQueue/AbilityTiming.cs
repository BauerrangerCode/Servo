using System.Text.RegularExpressions;
using Servitor.App.BattleTracker.State;

namespace Servitor.App.BattleTracker.ActionQueue;

/// <summary>
/// Reads when an ability or army rule applies from the first sentence of its text ("In your Shooting phase, ...",
/// "At the start of the battle round, ..."). The datasheets carry no structured timing, so this is a text heuristic:
/// text without a recognisable phase or moment yields null, i.e. a passive ability that is always in effect.
/// </summary>
public static class AbilityTiming
{
    private const string Phases = "Command|Movement|Shooting|Charge|Fight";

    private static readonly Regex RoundMoment = new(@"\b(?<edge>start|beginning|end) of the (first |next )?battle round", RegexOptions.IgnoreCase);
    private static readonly Regex TurnMoment = new(@"\b(?<edge>start|beginning|end) of (?<who>your opponent.s|your|the) turn", RegexOptions.IgnoreCase);
    private static readonly Regex PhaseMention = new($@"\b(?<who>your opponent.s|your|the opponent.s|an enemy|the) (?<phases>(?:{Phases})(?: or (?:{Phases}))*) phase", RegexOptions.IgnoreCase);
    private static readonly Regex SentenceEnd = new(@"[.:!?]\s");

    public static Timing? Of(string text)
    {
        var sentence = FirstSentence(text);

        if (RoundMoment.Match(sentence) is { Success: true } round)
        {
            return Moment(IsEnd(round) ? TimingMoment.RoundEnd : TimingMoment.RoundStart, TurnScope.Either);
        }

        if (PhaseMention.Match(sentence) is { Success: true } phase)
        {
            var phases = Enum.GetValues<BattlePhase>()
                .Where(p => phase.Groups["phases"].Value.Contains(BattlePhaseLabels.Label(p), StringComparison.OrdinalIgnoreCase))
                .ToHashSet();
            return new Timing(ScopeOf(phase.Groups["who"].Value), phases, false, TimingMoment.None);
        }

        if (TurnMoment.Match(sentence) is { Success: true } turn)
        {
            return Moment(IsEnd(turn) ? TimingMoment.TurnEnd : TimingMoment.TurnStart, ScopeOf(turn.Groups["who"].Value));
        }

        return null;
    }

    private static Timing Moment(TimingMoment moment, TurnScope scope) =>
        new(scope, Enum.GetValues<BattlePhase>().ToHashSet(), true, moment);

    private static bool IsEnd(Match match) => match.Groups["edge"].Value.Equals("end", StringComparison.OrdinalIgnoreCase);

    private static TurnScope ScopeOf(string who) =>
        who.StartsWith("your opponent", StringComparison.OrdinalIgnoreCase) || who.Contains("opponent", StringComparison.OrdinalIgnoreCase) || who.Contains("enemy", StringComparison.OrdinalIgnoreCase)
            ? TurnScope.Opponent
            : who.Equals("your", StringComparison.OrdinalIgnoreCase) ? TurnScope.Your : TurnScope.Either;

    private static string FirstSentence(string text)
    {
        var normalized = text.Trim();
        var end = SentenceEnd.Match(normalized);
        return end.Success ? normalized[..end.Index] : normalized;
    }
}
