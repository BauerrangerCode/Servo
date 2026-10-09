using Servitor.App.Scoring.MissionDeck;

namespace Servitor.App.Scoring.Tracking;

public static class VpCalculator
{
    public static SideScore Calculate(PrimaryMission? mission, ScoreSheet sheet, MissionDeckData deck)
    {
        var (primaryRounds, endOfBattle, primary) = Primary(mission, sheet);
        var (secondaryRounds, secondary) = Secondary(sheet, deck);
        return new SideScore(
            primaryRounds,
            endOfBattle,
            primary,
            secondaryRounds,
            secondary,
            sheet.Painted ? ScoringRules.PaintVp : 0,
            sheet.Challenger);
    }

    private static (int[] Rounds, int EndOfBattle, int Total) Primary(PrimaryMission? mission, ScoreSheet sheet)
    {
        var rounds = new int[ScoringRules.BattleRounds];
        var endOfBattle = 0;
        if (mission is not null)
        {
            foreach (var ((round, index), count) in sheet.PrimaryCounts)
            {
                var condition = mission.ScoringConditions[index];
                if (condition.IsEndOfBattle)
                {
                    endOfBattle += count * condition.Points;
                }
                else
                {
                    rounds[round - 1] += count * condition.Points;
                }
            }
        }

        var capped = rounds.Select(vp => Math.Min(vp, ScoringRules.MaxVpPerRound)).ToArray();
        return (capped, endOfBattle, Math.Min(capped.Sum() + endOfBattle, ScoringRules.PrimaryMaxVp));
    }

    private static (int[] Rounds, int Total) Secondary(ScoreSheet sheet, MissionDeckData deck)
    {
        var rounds = new int[ScoringRules.BattleRounds];
        var scoringCards = (sheet.Mode == SecondaryMode.Fixed ? sheet.FixedPicks : [.. sheet.ActiveCards, .. sheet.DiscardedCards])
            .Select(deck.FindSecondary)
            .OfType<SecondaryMission>()
            .ToList();
        var cardTotals = scoringCards.ToDictionary(c => c.Name, _ => 0);

        // VP over a limit are ignored, so only the VP actually counted in a round use up a Fixed card's 20VP.
        for (var round = 1; round <= ScoringRules.BattleRounds; round++)
        {
            var roundRoom = ScoringRules.MaxVpPerRound;
            foreach (var card in scoringCards)
            {
                var cardRoom = sheet.Mode == SecondaryMode.Fixed ? ScoringRules.FixedCardMaxVp - cardTotals[card.Name] : int.MaxValue;
                var counted = Math.Min(CardVpInRound(card, sheet, round), Math.Min(cardRoom, roundRoom));
                cardTotals[card.Name] += counted;
                roundRoom -= counted;
            }

            rounds[round - 1] = ScoringRules.MaxVpPerRound - roundRoom;
        }

        return (rounds, Math.Min(rounds.Sum(), ScoringRules.SecondaryMaxVp));
    }

    private static int CardVpInRound(SecondaryMission card, ScoreSheet sheet, int round)
    {
        var vp = 0;
        for (var index = 0; index < card.Conditions.Count; index++)
        {
            if (sheet.SecondaryCounts.TryGetValue((card.Name, round, index), out var count) && card.Conditions[index].PointsIn(sheet.Mode) is { } points)
            {
                vp += count * points;
            }
        }

        return vp;
    }
}
