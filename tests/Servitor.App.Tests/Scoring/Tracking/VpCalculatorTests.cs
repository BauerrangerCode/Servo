using Servitor.App.Scoring.MissionDeck;
using Servitor.App.Scoring.Tracking;

namespace Servitor.App.Tests.Scoring.Tracking;

public class VpCalculatorTests
{
    private static SideScore Calculate(ScoreSheet sheet) => VpCalculator.Calculate(ScoringFixtures.YourMission, sheet, ScoringFixtures.Deck());

    [Fact]
    public void An_empty_sheet_scores_nothing()
    {
        var score = Calculate(new ScoreSheet());

        Assert.Equal(0, score.Total);
        Assert.Equal(new int[5], score.PrimaryRounds);
    }

    [Fact]
    public void Primary_vp_is_count_times_points_per_round()
    {
        var sheet = new ScoreSheet();
        sheet.PrimaryCounts[(1, ScoringFixtures.CountableIndex)] = 2;
        sheet.PrimaryCounts[(1, ScoringFixtures.YesNoIndex)] = 1;
        sheet.PrimaryCounts[(2, ScoringFixtures.CountableIndex)] = 1;

        var score = Calculate(sheet);

        Assert.Equal([12, 5, 0, 0, 0], score.PrimaryRounds);
        Assert.Equal(17, score.Primary);
    }

    [Fact]
    public void A_round_is_capped_at_15_vp()
    {
        var sheet = new ScoreSheet();
        sheet.PrimaryCounts[(1, ScoringFixtures.CountableIndex)] = 5;

        var score = Calculate(sheet);

        Assert.Equal(15, score.PrimaryRounds[0]);
        Assert.Equal(15, score.Primary);
    }

    [Fact]
    public void End_of_battle_vp_is_outside_the_round_cap_but_inside_the_primary_cap()
    {
        var sheet = new ScoreSheet();
        sheet.PrimaryCounts[(5, ScoringFixtures.CountableIndex)] = 3;
        sheet.PrimaryCounts[(5, ScoringFixtures.EndOfBattleIndex)] = 1;

        var score = Calculate(sheet);

        Assert.Equal(15, score.PrimaryRounds[4]);
        Assert.Equal(8, score.PrimaryEndOfBattle);
        Assert.Equal(23, score.Primary);
    }

    [Fact]
    public void Primary_is_capped_at_45_vp()
    {
        var sheet = new ScoreSheet();
        for (var round = 1; round <= 5; round++)
        {
            sheet.PrimaryCounts[(round, ScoringFixtures.CountableIndex)] = 3;
        }

        sheet.PrimaryCounts[(5, ScoringFixtures.EndOfBattleIndex)] = 1;

        Assert.Equal(45, Calculate(sheet).Primary);
    }

    [Fact]
    public void A_tactical_card_scores_its_tactical_value_and_the_fixed_value_in_fixed_mode()
    {
        var sheet = new ScoreSheet { Mode = SecondaryMode.Tactical };
        sheet.ActiveCards.Add("Alpha");
        sheet.SecondaryCounts[("Alpha", 1, 0)] = 1;
        Assert.Equal(5, Calculate(sheet).Secondary);

        sheet.Mode = SecondaryMode.Fixed;
        sheet.FixedPicks.Add("Alpha");
        Assert.Equal(4, Calculate(sheet).Secondary);
    }

    [Fact]
    public void Conditions_without_a_value_in_the_current_mode_score_nothing()
    {
        var sheet = new ScoreSheet { Mode = SecondaryMode.Tactical };
        sheet.ActiveCards.Add("Delta");
        sheet.SecondaryCounts[("Delta", 1, 0)] = 4;
        sheet.SecondaryCounts[("Delta", 1, 1)] = 1;

        Assert.Equal(5, Calculate(sheet).Secondary);
    }

    [Fact]
    public void Only_the_cards_of_the_current_mode_count()
    {
        var sheet = new ScoreSheet { Mode = SecondaryMode.Fixed };
        sheet.FixedPicks.Add("Beta");
        sheet.ActiveCards.Add("Alpha");
        sheet.SecondaryCounts[("Beta", 1, 0)] = 1;
        sheet.SecondaryCounts[("Alpha", 1, 0)] = 1;

        Assert.Equal(3, Calculate(sheet).Secondary);
    }

    [Fact]
    public void A_discarded_tactical_card_keeps_its_vp()
    {
        var sheet = new ScoreSheet { Mode = SecondaryMode.Tactical };
        sheet.DiscardedCards.Add("Beta");
        sheet.SecondaryCounts[("Beta", 2, 0)] = 1;

        Assert.Equal(3, Calculate(sheet).Secondary);
    }

    [Fact]
    public void A_fixed_card_is_capped_at_20_vp_over_the_battle()
    {
        var sheet = new ScoreSheet { Mode = SecondaryMode.Fixed };
        sheet.FixedPicks.Add("Alpha");
        for (var round = 1; round <= 5; round++)
        {
            sheet.SecondaryCounts[("Alpha", round, 0)] = 3;
        }

        var score = Calculate(sheet);

        Assert.Equal(20, score.Secondary);
        Assert.Equal([12, 8, 0, 0, 0], score.SecondaryRounds);
    }

    [Fact]
    public void Fixed_vp_cut_off_by_the_round_limit_do_not_use_up_the_cards_20_vp()
    {
        var sheet = new ScoreSheet { Mode = SecondaryMode.Fixed };
        sheet.FixedPicks.Add("Alpha");
        sheet.SecondaryCounts[("Alpha", 1, 0)] = 6;
        sheet.SecondaryCounts[("Alpha", 2, 0)] = 3;

        var score = Calculate(sheet);

        Assert.Equal([15, 5, 0, 0, 0], score.SecondaryRounds);
        Assert.Equal(20, score.Secondary);
    }

    [Fact]
    public void Two_fixed_cards_share_the_round_limit_in_pick_order()
    {
        var sheet = new ScoreSheet { Mode = SecondaryMode.Fixed };
        sheet.FixedPicks.AddRange(["Alpha", "Beta"]);
        sheet.SecondaryCounts[("Alpha", 1, 0)] = 3;
        sheet.SecondaryCounts[("Beta", 1, 0)] = 3;

        var score = Calculate(sheet);

        Assert.Equal(15, score.SecondaryRounds[0]);
    }

    [Fact]
    public void Secondary_rounds_are_capped_at_15_and_the_total_at_45()
    {
        var sheet = new ScoreSheet { Mode = SecondaryMode.Tactical };
        sheet.ActiveCards.Add("Alpha");
        for (var round = 1; round <= 5; round++)
        {
            sheet.SecondaryCounts[("Alpha", round, 0)] = 4;
        }

        var score = Calculate(sheet);

        Assert.All(score.SecondaryRounds, vp => Assert.Equal(15, vp));
        Assert.Equal(45, score.Secondary);
    }

    [Fact]
    public void Paint_and_challenger_are_added_on_top_without_a_cap()
    {
        var sheet = new ScoreSheet { Painted = true, Challenger = 3 };

        var score = Calculate(sheet);

        Assert.Equal(10, score.Paint);
        Assert.Equal(13, score.Total);
    }

    [Fact]
    public void Without_a_mission_only_secondary_paint_and_challenger_count()
    {
        var sheet = new ScoreSheet { Painted = true };

        var score = VpCalculator.Calculate(null, sheet, ScoringFixtures.Deck());

        Assert.Equal(10, score.Total);
    }
}
