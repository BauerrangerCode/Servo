using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.BattleTracker.State;
using Servitor.App.Scoring.Components;
using Servitor.App.Scoring.History;
using Servitor.App.Scoring.MissionDeck;
using Servitor.App.Scoring.State;
using Servitor.App.Scoring.Tracking;
using Servitor.App.Tests.RulesData.Loading;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.Scoring.History;

public class ScorecardHistoryTests : BunitContext
{
    private readonly FakeScorecardStore _store = new();
    private readonly GameRecorder _recorder;
    private readonly BattleSetupState _setup;
    private readonly ScoreTrackingState _tracking;

    public ScorecardHistoryTests()
    {
        (_setup, _tracking) = ScoringFixtures.Prepared();
        _recorder = new GameRecorder(_setup, _tracking, new BattleProgressState(new InMemoryRulesDataStore()), _store, TimeProvider.System);
        Services.AddSingleton<IScorecardStore>(_store);
        Services.AddSingleton<IGameRecorder>(_recorder);
    }

    private static Scorecard Card(string mission, int you, int enemy, DateTimeOffset endedAt, GameResult result) => new(
        Guid.NewGuid(),
        endedAt,
        MissionArchetype.TakeAndHold,
        MissionArchetype.PurgeTheFoe,
        mission,
        "Unstoppable Force",
        "Dawn of War",
        BattleRole.Attacker,
        Side.You,
        SideCard(you),
        SideCard(enemy),
        result);

    private static SideScorecard SideCard(int total) => new([total, 0, 0, 0, 0], 0, total, SecondaryMode.Tactical, [0, 0, 0, 0, 0], 0, 0, 0, total);

    [Fact]
    public void An_empty_history_says_so()
    {
        var cut = Render<ScorecardHistory>();

        Assert.Contains("No finished games yet", cut.Find(".history-empty").TextContent);
    }

    [Fact]
    public async Task Saved_games_are_listed_newest_first_with_result_score_and_mission()
    {
        await _store.SaveAsync(Card("Older Mission", 5, 9, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), GameResult.Defeat));
        await _store.SaveAsync(Card("Newer Mission", 20, 9, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), GameResult.Victory));

        var cut = Render<ScorecardHistory>();

        var entries = cut.FindAll(".history-entry");
        Assert.Equal(2, entries.Count);
        Assert.Contains("Newer Mission", entries[0].TextContent);
        Assert.Contains("Victory", entries[0].TextContent);
        Assert.Contains("You 20 — Enemy 9", entries[0].TextContent);
        Assert.Contains("Take and Hold vs Purge the Foe", entries[0].TextContent);
        Assert.Contains("Older Mission", entries[1].TextContent);
        Assert.Contains("history-defeat", entries[1].ClassName);
    }

    [Fact]
    public async Task Details_show_the_per_round_and_per_category_vp_and_the_setup()
    {
        await _store.SaveAsync(Card("Immovable Object", 7, 3, DateTimeOffset.UnixEpoch, GameResult.Victory));
        var cut = Render<ScorecardHistory>();
        Assert.Empty(cut.FindAll(".history-details"));

        cut.Find(".history-toggle").Click();

        var details = cut.Find(".history-details");
        Assert.Contains("Enemy mission: Unstoppable Force", details.TextContent);
        Assert.Contains("Layout: Dawn of War", details.TextContent);
        Assert.Contains("You were the Attacker", details.TextContent);
        Assert.Contains("First turn: You", details.TextContent);
        Assert.Equal(["R1", "R2", "R3", "R4", "R5", "End", "Total"], details.QuerySelectorAll("thead th").Skip(1).Select(t => t.TextContent.Trim()));
        Assert.Equal("7", cut.Find(".history-primary td").TextContent);

        cut.Find(".history-toggle").Click();
        Assert.Empty(cut.FindAll(".history-details"));
    }

    [Fact]
    public async Task Deleting_needs_a_confirmation_and_removes_the_entry()
    {
        await _store.SaveAsync(Card("To Delete", 1, 2, DateTimeOffset.UnixEpoch, GameResult.Defeat));
        var cut = Render<ScorecardHistory>();

        cut.Find(".history-delete").Click();
        cut.Find(".history-delete-cancel").Click();
        Assert.Single(cut.FindAll(".history-entry"));

        cut.Find(".history-delete").Click();
        cut.Find(".history-delete-yes").Click();

        cut.WaitForAssertion(() => Assert.Contains("No finished games yet", cut.Markup));
        Assert.Empty(_store.Scorecards);
    }

    [Fact]
    public async Task Ending_a_game_adds_it_to_the_open_history()
    {
        var cut = Render<ScorecardHistory>();
        Assert.Empty(cut.FindAll(".history-entry"));
        _tracking.AdjustPrimary(Side.You, ScoringFixtures.CountableIndex, 1);

        await cut.InvokeAsync(() => _recorder.EndGameAsync());

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".history-entry")));
        Assert.Contains("You 5 — Enemy 0", cut.Find(".history-entry").TextContent);
    }
}
