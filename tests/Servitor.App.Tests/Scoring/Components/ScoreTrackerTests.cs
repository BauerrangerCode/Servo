using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.Scoring.Components;
using Servitor.App.Scoring.Tracking;

namespace Servitor.App.Tests.Scoring.Components;

public class ScoreTrackerTests : BunitContext
{
    private readonly ScoreTrackingState _tracking;

    public ScoreTrackerTests()
    {
        _tracking = ScoringFixtures.Prepared().Tracking;
        Services.AddSingleton<IScoreTrackingState>(_tracking);
    }

    private IRenderedComponent<ScoreTracker> RenderTracker() => Render<ScoreTracker>();

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
}
