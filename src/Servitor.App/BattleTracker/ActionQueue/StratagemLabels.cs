using Servitor.App.BattleTracker.State;
using Servitor.RulesData.Domain;

namespace Servitor.App.BattleTracker.ActionQueue;

public static class StratagemLabels
{
    public static string Category(StratagemCategory category) => category switch
    {
        StratagemCategory.BattleTactic => "Battle Tactic",
        StratagemCategory.StrategicPloy => "Strategic Ploy",
        StratagemCategory.EpicDeed => "Epic Deed",
        StratagemCategory.Wargear => "Wargear",
        StratagemCategory.Other => "Stratagem",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    /// <summary>E.g. "Your Shooting phase", "Enemy Shooting or Fight phase", "Any Fight phase", "End of your turn".</summary>
    public static string Timing(Timing timing)
    {
        var who = timing.Scope switch
        {
            TurnScope.Your => "your",
            TurnScope.Opponent => "enemy",
            _ => "any",
        };

        return timing.Moment switch
        {
            TimingMoment.RoundStart => "Start of battle round",
            TimingMoment.RoundEnd => "End of battle round",
            TimingMoment.TurnStart => $"Start of {who} turn",
            TimingMoment.TurnEnd => $"End of {who} turn",
            TimingMoment.TurnStartOrEnd => $"Start or end of {who} turn",
            _ when timing.AnyPhase => AnyPhaseLabel(timing.Scope),
            _ => $"{Capitalized(who)} {string.Join(" or ", timing.Phases.Order().Select(BattlePhaseLabels.Label))} phase",
        };
    }

    private static string AnyPhaseLabel(TurnScope scope) => scope switch
    {
        TurnScope.Your => "Your turn, any phase",
        TurnScope.Opponent => "Enemy turn, any phase",
        _ => "Any phase",
    };

    private static string Capitalized(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}
