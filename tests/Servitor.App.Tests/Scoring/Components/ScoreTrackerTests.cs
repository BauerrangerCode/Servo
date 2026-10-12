using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.BattleTracker.State;
using Servitor.App.Scoring.Components;
using Servitor.App.Scoring.History;
using Servitor.App.Scoring.Tracking;
using Servitor.App.Shared.Pager;
using Servitor.App.Tests.Shared.Pager;
using Servitor.App.Tests.BattleTracker;
using Servitor.App.Tests.RulesData.Loading;
using Servitor.App.Tests.Scoring.History;

namespace Servitor.App.Tests.Scoring.Components;

public class ScoreTrackerTests : BunitContext
{
    private readonly ScoreTrackingState _tracking;
    private readonly FakeScorecardStore _scorecards = new();
    private readonly Servitor.App.Scoring.State.BattleSetupState _setup;

    public ScoreTrackerTests()
    {
        var (setup, tracking) = ScoringFixtures.Prepared();
        _tracking = tracking;
        _setup = setup;
        Services.AddSingleton<IScoreTrackingState>(_tracking);
        Services.AddSingleton<IPagerInterop, FakePagerInterop>();
        Services.AddSingleton<IScorecardStore>(_scorecards);
        Services.AddSingleton<IGameRecorder>(new GameRecorder(setup, tracking, new BattleProgressState(new InMemoryRulesDataStore()), new FakeBattlePlaySession(), _scorecards, TimeProvider.System));
    }

    // ScoreTracker emits its screens into the enclosing pager, so it is rendered inside one.
    private IRenderedComponent<ScreenPager> RenderTracker() =>
        Render<ScreenPager>(parameters => parameters.AddChildContent<ScoreTracker>());

    private static string YouPrimaryEntry(int number, string part) => $".side-you .primary-panel .entry-list li:nth-child({number}) {part}";

    [Fact]
    public void Both_sides_start_at_zero_and_tied()
    {
        var cut = RenderTracker();

        Assert.Contains("Tied", cut.Find(".lead-indicator").TextContent);
        Assert.Equal("0 VP", cut.Find(".side-you .side-total").TextContent.Trim());
        Assert.Equal("0 VP", cut.Find(".side-enemy .side-total").TextContent.Trim());
    }

    [Fact]
    public void Counting_a_condition_up_updates_the_side_total_the_header_and_the_history()
    {
        var cut = RenderTracker();

        cut.Find(YouPrimaryEntry(1, ".step-up")).Click();
        cut.Find(YouPrimaryEntry(1, ".step-up")).Click();

        Assert.Equal("10 VP", cut.Find(".side-you .side-total").TextContent.Trim());
        Assert.Contains("You lead by 10", cut.Find(".lead-indicator").TextContent);
        Assert.Equal("10", cut.Find(".history-you-primary td").TextContent);
        Assert.Contains("Primary — Immovable Object", cut.Find(".side-you .primary-panel h3").TextContent);
        Assert.Contains("10/45 VP", cut.Find(".side-you .primary-panel h3").TextContent);
    }

    [Fact]
    public void A_yes_no_condition_is_a_checkbox()
    {
        var cut = RenderTracker();

        cut.Find(YouPrimaryEntry(2, ".entry-check")).Change(true);

        Assert.Equal("2 VP", cut.Find(".side-you .side-total").TextContent.Trim());
    }

    [Fact]
    public void The_end_of_battle_entry_is_disabled_until_round_five_is_selected()
    {
        var cut = RenderTracker();

        Assert.True(cut.Find(YouPrimaryEntry(3, ".entry-check")).HasAttribute("disabled"));

        cut.FindAll(".round-chip")[4].Click();

        Assert.False(cut.Find(YouPrimaryEntry(3, ".entry-check")).HasAttribute("disabled"));
    }

    [Fact]
    public void A_condition_that_starts_in_round_two_is_disabled_in_round_one()
    {
        var cut = RenderTracker();

        Assert.True(cut.Find(YouPrimaryEntry(4, ".entry-check")).HasAttribute("disabled"));

        cut.FindAll(".round-chip")[1].Click();

        Assert.False(cut.Find(YouPrimaryEntry(4, ".entry-check")).HasAttribute("disabled"));
    }

    [Fact]
    public void The_selected_round_is_highlighted_and_scores_land_in_that_round()
    {
        var cut = RenderTracker();

        cut.FindAll(".round-chip")[2].Click();
        cut.Find(YouPrimaryEntry(1, ".step-up")).Click();

        Assert.Contains("selected", cut.FindAll(".round-chip")[2].ClassName);
        Assert.Equal("5", cut.FindAll(".history-you-primary td")[2].TextContent);
        Assert.Contains("Round 3: 5/15 VP", cut.Find(".side-you .round-subtotal").TextContent);
    }

    [Fact]
    public void The_enemy_panel_shows_the_enemy_mission_and_scores_separately()
    {
        var cut = RenderTracker();

        Assert.Contains("Unstoppable Force", cut.Find(".side-enemy .primary-panel h3").TextContent);

        cut.Find(".side-enemy .primary-panel .step-up").Click();

        Assert.Equal("4 VP", cut.Find(".side-enemy .side-total").TextContent.Trim());
        Assert.Equal("0 VP", cut.Find(".side-you .side-total").TextContent.Trim());
        Assert.Contains("Enemy leads by 4", cut.Find(".lead-indicator").TextContent);
    }

    [Fact]
    public void Paint_adds_ten_and_the_challenger_counter_is_a_fourth_term()
    {
        var cut = RenderTracker();

        cut.Find(".side-you .paint-check").Change(true);
        cut.Find(".side-you .chall-up").Click();

        Assert.Equal("11 VP", cut.Find(".side-you .side-total").TextContent.Trim());
        Assert.Equal("0 + 0 + 10 + 1", cut.Find(".side-you .side-breakdown").TextContent.Trim());
    }

    [Fact]
    public void Tactical_is_the_default_and_drawing_shows_a_card_with_its_entries()
    {
        var cut = RenderTracker();
        Assert.Contains("4 cards remaining", cut.Find(".side-you .deck-info").TextContent);

        cut.Find(".side-you .draw-card").Click();

        Assert.Contains("3 cards remaining", cut.Find(".side-you .deck-info").TextContent);
        Assert.Single(cut.FindAll(".side-you .secondary-card"));
    }

    [Fact]
    public void A_drawn_card_can_be_scored_and_discarded_and_the_replacement_is_spent_once()
    {
        var cut = RenderTracker();
        cut.Find(".side-you .add-card").Change("Beta");

        cut.Find(".side-you .secondary-card .step-up, .side-you .secondary-card .entry-check").Change(true);
        Assert.Contains("3/45 VP", cut.Find(".side-you .secondary-panel h3").TextContent);

        cut.Find(".side-you .discard-card").Click();

        Assert.Empty(cut.FindAll(".side-you .secondary-card"));
        Assert.Contains("Discarded: Beta", cut.Find(".side-you .discarded-cards").TextContent);
        Assert.Contains("3/45 VP", cut.Find(".side-you .secondary-panel h3").TextContent);
    }

    [Fact]
    public void Fixed_mode_offers_the_fixed_cards_and_allows_two_picks_only()
    {
        var cut = RenderTracker();
        cut.Find(".side-you .mode-fixed").Click();

        var choices = cut.FindAll(".side-you .fixed-choice");
        Assert.Equal(["Alpha", "Beta", "Gamma"], choices.Select(c => c.TextContent.Trim()));

        choices[0].Click();
        cut.FindAll(".side-you .fixed-choice")[1].Click();
        cut.FindAll(".side-you .fixed-choice")[2].Click();

        Assert.Equal(2, cut.FindAll(".side-you .fixed-choice.selected").Count);
        Assert.Equal(2, cut.FindAll(".side-you .secondary-card").Count);
        Assert.Contains("(2/2)", cut.Find(".side-you .mode-hint").TextContent);
    }

    [Fact]
    public void A_fixed_card_scores_its_fixed_value()
    {
        var cut = RenderTracker();
        cut.Find(".side-you .mode-fixed").Click();
        cut.FindAll(".side-you .fixed-choice")[0].Click();

        cut.Find(".side-you .secondary-card .step-up").Click();

        Assert.Contains("4/45 VP", cut.Find(".side-you .secondary-panel h3").TextContent);
        Assert.Equal("4", cut.Find(".history-you-secondary td").TextContent);
    }

    [Fact]
    public void End_game_asks_for_confirmation_and_cancel_keeps_the_game_running()
    {
        var cut = RenderTracker();
        cut.Find(YouPrimaryEntry(1, ".step-up")).Click();

        cut.Find(".end-game-button").Click();

        Assert.Contains("You 5 — Enemy 0", cut.Find(".end-game-confirm-text").TextContent);
        cut.Find(".end-game-cancel").Click();
        Assert.Empty(cut.FindAll(".end-game-confirm"));
        Assert.Empty(_scorecards.Scorecards);
        Assert.Equal(5, _tracking.ScoreOf(Side.You).Total);
    }

    [Fact]
    public void Confirming_end_game_saves_the_scorecard_and_clears_the_game()
    {
        var cut = RenderTracker();
        cut.Find(YouPrimaryEntry(1, ".step-up")).Click();

        cut.Find(".end-game-button").Click();
        cut.Find(".end-game-confirm").Click();

        var card = Assert.Single(_scorecards.Scorecards);
        Assert.Equal(GameResult.Victory, card.Result);
        Assert.Equal(5, card.You.Total);
        Assert.Null(_setup.YourDisposition);
        Assert.Equal(0, _tracking.ScoreOf(Side.You).Total);
    }

    [Fact]
    public void A_failing_save_keeps_the_game_and_shows_why()
    {
        _scorecards.FailSavesWith = new Microsoft.JSInterop.JSException("storage unavailable");
        var cut = RenderTracker();
        cut.Find(YouPrimaryEntry(1, ".step-up")).Click();

        cut.Find(".end-game-button").Click();
        cut.Find(".end-game-confirm").Click();

        Assert.Contains("storage unavailable", cut.Find(".end-game-error").TextContent);
        Assert.Empty(_scorecards.Scorecards);
        Assert.Equal(5, _tracking.ScoreOf(Side.You).Total);
        Assert.NotNull(_setup.YourDisposition);
    }
}
