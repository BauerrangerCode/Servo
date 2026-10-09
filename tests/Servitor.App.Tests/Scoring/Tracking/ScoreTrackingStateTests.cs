using Servitor.App.Scoring.MissionDeck;
using Servitor.App.Scoring.Tracking;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.Scoring.Tracking;

public class ScoreTrackingStateTests
{
    private static ScoreTrackingState Tracking(int seed = 1) => ScoringFixtures.Prepared(seed).Tracking;

    // ---- who scores which mission ------------------------------------------------------------------------------

    [Fact]
    public void Each_side_scores_its_own_mission_the_enemy_one_being_the_swapped_pair()
    {
        var tracking = Tracking();

        Assert.Same(ScoringFixtures.YourMission, tracking.MissionOf(Side.You));
        Assert.Same(ScoringFixtures.EnemyMission, tracking.MissionOf(Side.Enemy));
    }

    // ---- primary entries ---------------------------------------------------------------------------------------

    [Fact]
    public void A_countable_condition_can_be_counted_up_and_down_but_not_below_zero()
    {
        var tracking = Tracking();

        Assert.True(tracking.AdjustPrimary(Side.You, ScoringFixtures.CountableIndex, 1));
        Assert.True(tracking.AdjustPrimary(Side.You, ScoringFixtures.CountableIndex, 1));
        Assert.Equal(2, tracking.PrimaryCount(Side.You, 1, ScoringFixtures.CountableIndex));
        Assert.Equal(10, tracking.ScoreOf(Side.You).Primary);

        Assert.True(tracking.AdjustPrimary(Side.You, ScoringFixtures.CountableIndex, -2));
        Assert.False(tracking.AdjustPrimary(Side.You, ScoringFixtures.CountableIndex, -1));
        Assert.Equal(0, tracking.ScoreOf(Side.You).Total);
    }

    [Fact]
    public void A_yes_no_condition_can_only_be_checked_once()
    {
        var tracking = Tracking();

        Assert.True(tracking.AdjustPrimary(Side.You, ScoringFixtures.YesNoIndex, 1));
        Assert.False(tracking.AdjustPrimary(Side.You, ScoringFixtures.YesNoIndex, 1));
        Assert.Equal(1, tracking.PrimaryCount(Side.You, 1, ScoringFixtures.YesNoIndex));
    }

    [Fact]
    public void An_end_of_battle_condition_is_refused_until_the_last_round_is_reached()
    {
        var tracking = Tracking();

        Assert.False(tracking.AdjustPrimary(Side.You, ScoringFixtures.EndOfBattleIndex, 1));

        tracking.SetRound(5);
        Assert.True(tracking.AdjustPrimary(Side.You, ScoringFixtures.EndOfBattleIndex, 1));
        Assert.Equal(8, tracking.ScoreOf(Side.You).PrimaryEndOfBattle);
    }

    [Fact]
    public void A_condition_that_starts_in_round_two_is_refused_in_round_one()
    {
        var tracking = Tracking();

        Assert.False(tracking.AdjustPrimary(Side.You, ScoringFixtures.SecondRoundOnwardsIndex, 1));

        tracking.SetRound(2);
        Assert.True(tracking.AdjustPrimary(Side.You, ScoringFixtures.SecondRoundOnwardsIndex, 1));
    }

    [Fact]
    public void Entries_are_recorded_in_the_selected_round()
    {
        var tracking = Tracking();
        tracking.SetRound(3);

        tracking.AdjustPrimary(Side.You, ScoringFixtures.CountableIndex, 1);

        Assert.Equal(1, tracking.PrimaryCount(Side.You, 3, ScoringFixtures.CountableIndex));
        Assert.Equal(0, tracking.PrimaryCount(Side.You, 1, ScoringFixtures.CountableIndex));
        Assert.Equal(5, tracking.ScoreOf(Side.You).PrimaryRounds[2]);
    }

    [Fact]
    public void An_unknown_condition_index_or_round_is_ignored()
    {
        var tracking = Tracking();

        Assert.False(tracking.AdjustPrimary(Side.You, 99, 1));
        tracking.SetRound(6);
        tracking.SetRound(0);

        Assert.Equal(1, tracking.CurrentRound);
    }

    [Fact]
    public void The_two_sides_are_tracked_independently()
    {
        var tracking = Tracking();

        tracking.AdjustPrimary(Side.You, ScoringFixtures.CountableIndex, 1);
        tracking.AdjustPrimary(Side.Enemy, 0, 2);

        Assert.Equal(5, tracking.ScoreOf(Side.You).Total);
        Assert.Equal(8, tracking.ScoreOf(Side.Enemy).Total);
    }

    [Fact]
    public void Paint_and_challenger_are_manual_and_the_challenger_never_goes_negative()
    {
        var tracking = Tracking();

        tracking.SetPainted(Side.You, true);
        tracking.AdjustChallenger(Side.You, 2);
        tracking.AdjustChallenger(Side.You, -5);

        var score = tracking.ScoreOf(Side.You);
        Assert.Equal(10, score.Paint);
        Assert.Equal(0, score.Challenger);
    }

    // ---- fixed secondary missions ------------------------------------------------------------------------------

    [Fact]
    public void Exactly_two_fixed_missions_can_be_picked_and_only_fixed_eligible_ones()
    {
        var tracking = Tracking();
        tracking.SetMode(Side.You, SecondaryMode.Fixed);

        Assert.True(tracking.ToggleFixedPick(Side.You, "Alpha"));
        Assert.True(tracking.ToggleFixedPick(Side.You, "Beta"));
        Assert.False(tracking.ToggleFixedPick(Side.You, "Gamma"));
        Assert.False(tracking.ToggleFixedPick(Side.You, "Delta"));

        Assert.True(tracking.ToggleFixedPick(Side.You, "Alpha"));
        Assert.True(tracking.ToggleFixedPick(Side.You, "Gamma"));
        Assert.Equal(["Beta", "Gamma"], tracking.FixedPicks(Side.You));
    }

    [Fact]
    public void Only_picked_fixed_cards_can_score_and_with_the_fixed_value()
    {
        var tracking = Tracking();
        tracking.SetMode(Side.You, SecondaryMode.Fixed);

        Assert.False(tracking.AdjustSecondary(Side.You, "Alpha", 0, 1));

        tracking.ToggleFixedPick(Side.You, "Alpha");
        Assert.True(tracking.AdjustSecondary(Side.You, "Alpha", 0, 1));
        Assert.Equal(4, tracking.ScoreOf(Side.You).Secondary);
    }

    [Fact]
    public void A_condition_without_a_value_in_the_mode_cannot_be_scored()
    {
        var tracking = Tracking();
        tracking.SetMode(Side.You, SecondaryMode.Tactical);
        tracking.AddCard(Side.You, "Delta");

        Assert.False(tracking.AdjustSecondary(Side.You, "Delta", 0, 1));
        Assert.True(tracking.AdjustSecondary(Side.You, "Delta", 1, 1));
    }

    [Fact]
    public void A_secondary_condition_is_refused_outside_its_rounds()
    {
        var tracking = Tracking();
        tracking.AddCard(Side.You, "Delta");

        Assert.False(tracking.AdjustSecondary(Side.You, "Delta", 2, 1));
        tracking.SetRound(2);
        Assert.True(tracking.AdjustSecondary(Side.You, "Delta", 2, 1));
    }

    // ---- tactical deck -----------------------------------------------------------------------------------------

    [Fact]
    public void Drawing_moves_a_card_from_the_deck_to_the_active_cards()
    {
        var tracking = Tracking();
        Assert.Equal(4, tracking.DeckRemaining(Side.You));

        var drawn = tracking.Draw(Side.You);

        Assert.NotNull(drawn);
        Assert.Equal(3, tracking.DeckRemaining(Side.You));
        Assert.Equal([drawn!.Name], tracking.ScoringCards(Side.You).Select(c => c.Name));
    }

    [Fact]
    public void The_deck_is_shuffled_per_side_and_runs_out_after_all_cards()
    {
        var tracking = Tracking();

        var drawn = Enumerable.Range(0, 4).Select(_ => tracking.Draw(Side.You)!.Name).ToList();

        Assert.Equal(["Alpha", "Beta", "Delta", "Gamma"], drawn.Order());
        Assert.Null(tracking.Draw(Side.You));
        Assert.Equal(4, tracking.DeckRemaining(Side.Enemy));
    }

    [Fact]
    public void The_same_seed_draws_the_same_order()
    {
        var first = Tracking(seed: 7);
        var second = Tracking(seed: 7);

        Assert.Equal(first.Draw(Side.You)!.Name, second.Draw(Side.You)!.Name);
    }

    [Fact]
    public void A_specific_card_can_be_added_from_the_deck_once()
    {
        var tracking = Tracking();

        Assert.True(tracking.AddCard(Side.You, "Gamma"));
        Assert.False(tracking.AddCard(Side.You, "Gamma"));
        Assert.False(tracking.AddCard(Side.You, "Unknown"));
        Assert.DoesNotContain(tracking.DeckCards(Side.You), c => c.Name == "Gamma");
        Assert.Equal(3, tracking.DeckRemaining(Side.You));
    }

    [Fact]
    public void A_discarded_card_stops_being_scoreable_but_keeps_its_vp()
    {
        var tracking = Tracking();
        tracking.AddCard(Side.You, "Beta");
        tracking.AdjustSecondary(Side.You, "Beta", 0, 1);

        Assert.True(tracking.Discard(Side.You, "Beta"));

        Assert.False(tracking.AdjustSecondary(Side.You, "Beta", 0, 1));
        Assert.Equal(3, tracking.ScoreOf(Side.You).Secondary);
        Assert.Equal(["Beta"], tracking.DiscardedCards(Side.You));
        Assert.False(tracking.Discard(Side.You, "Beta"));
    }

    [Fact]
    public void The_paid_replacement_works_once_per_battle()
    {
        var tracking = Tracking();
        tracking.AddCard(Side.You, "Alpha");
        tracking.AddCard(Side.You, "Beta");

        Assert.True(tracking.Replace(Side.You, "Alpha"));
        Assert.True(tracking.ReplaceUsed(Side.You));
        Assert.False(tracking.Replace(Side.You, "Beta"));

        Assert.Equal(["Alpha"], tracking.DiscardedCards(Side.You));
        Assert.Equal(2, tracking.ScoringCards(Side.You).Count);
        Assert.Equal(1, tracking.DeckRemaining(Side.You));
    }

    [Fact]
    public void The_replacement_is_refused_for_a_card_that_is_not_active()
    {
        var tracking = Tracking();

        Assert.False(tracking.Replace(Side.You, "Alpha"));
        Assert.False(tracking.ReplaceUsed(Side.You));
    }

    // ---- resetting ---------------------------------------------------------------------------------------------

    [Fact]
    public void Picking_different_dispositions_starts_the_scoring_clean()
    {
        var (setup, tracking) = ScoringFixtures.Prepared();
        tracking.AdjustPrimary(Side.You, ScoringFixtures.CountableIndex, 1);
        tracking.SetRound(3);
        Assert.Equal(5, tracking.ScoreOf(Side.You).Total);

        setup.SelectOpponentDisposition(MissionArchetype.TakeAndHold);

        Assert.Equal(0, tracking.ScoreOf(Side.You).Total);
        Assert.Equal(1, tracking.CurrentRound);
    }

    [Fact]
    public void Choosing_the_deployment_or_role_does_not_clear_the_scoring()
    {
        var (setup, tracking) = ScoringFixtures.Prepared();
        tracking.AdjustPrimary(Side.You, ScoringFixtures.CountableIndex, 1);

        setup.SelectDeploymentCard("Hammer and Anvil");
        setup.SelectRole(Servitor.App.Scoring.State.BattleRole.Attacker);

        Assert.Equal(5, tracking.ScoreOf(Side.You).Total);
    }

    [Fact]
    public void Changed_fires_for_accepted_entries_only()
    {
        var tracking = Tracking();
        var raised = 0;
        tracking.Changed += () => raised++;

        tracking.AdjustPrimary(Side.You, ScoringFixtures.CountableIndex, 1);
        tracking.AdjustPrimary(Side.You, ScoringFixtures.EndOfBattleIndex, 1);

        Assert.Equal(1, raised);
    }
}
