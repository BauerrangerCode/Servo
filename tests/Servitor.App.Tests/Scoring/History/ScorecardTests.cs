using System.Text.Json;
using Servitor.App.BattleTracker.State;
using Servitor.App.RulesData.Storage;
using Servitor.App.Scoring.History;
using Servitor.App.Scoring.MissionDeck;
using Servitor.App.Scoring.State;
using Servitor.App.Scoring.Tracking;
using Servitor.App.Tests.RulesData.Loading;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.Scoring.History;

public class ScorecardFactoryTests
{
    private static BattleProgressState NewProgress() => new(new InMemoryRulesDataStore());

    private static readonly DateTimeOffset Ended = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task The_scorecard_copies_missions_dispositions_setup_and_vp_per_round_and_category()
    {
        var (setup, tracking) = ScoringFixtures.Prepared();
        setup.SelectDeploymentCard("Hammer and Anvil");
        setup.SelectRole(BattleRole.Defender);
        var progress = NewProgress();
        await progress.SetFirstTurnAsync(Side.Enemy);
        await progress.NextPhaseAsync();
        tracking.AdjustPrimary(Side.You, ScoringFixtures.CountableIndex, 2);
        tracking.SetPainted(Side.You, true);
        tracking.AdjustChallenger(Side.Enemy, 1);
        tracking.SetRound(5);
        tracking.AdjustPrimary(Side.You, ScoringFixtures.EndOfBattleIndex, 1);

        var card = ScorecardFactory.Create(setup, tracking, progress, Ended)!;

        Assert.Equal(Ended, card.EndedAtUtc);
        Assert.Equal((MissionArchetype.TakeAndHold, MissionArchetype.PurgeTheFoe), (card.YourDisposition, card.OpponentDisposition));
        Assert.Equal(("Immovable Object", "Unstoppable Force"), (card.YourMission, card.OpponentMission));
        Assert.Equal(("Hammer and Anvil", BattleRole.Defender, Side.Enemy), (card.Layout, card.Role, card.FirstTurn));
        Assert.Equal([10, 0, 0, 0, 0], card.You.PrimaryRounds);
        Assert.Equal(8, card.You.PrimaryEndOfBattle);
        Assert.Equal((18, 0, 10, 0, 28), (card.You.Primary, card.You.Secondary, card.You.Paint, card.You.Challenger, card.You.Total));
        Assert.Equal((0, 1), (card.Enemy.Primary, card.Enemy.Challenger));
        Assert.Equal(SecondaryMode.Tactical, card.You.SecondaryMode);
    }

    [Theory]
    [InlineData(10, 4, GameResult.Victory)]
    [InlineData(4, 10, GameResult.Defeat)]
    [InlineData(7, 7, GameResult.Draw)]
    [InlineData(0, 0, GameResult.Draw)]
    public void The_result_follows_the_final_totals(int you, int enemy, GameResult expected)
    {
        Assert.Equal(expected, ScorecardFactory.ResultOf(you, enemy));
    }

    [Fact]
    public void A_tie_is_recorded_as_a_draw()
    {
        var (setup, tracking) = ScoringFixtures.Prepared();
        tracking.SetPainted(Side.You, true);
        tracking.SetPainted(Side.Enemy, true);

        Assert.Equal(GameResult.Draw, ScorecardFactory.Create(setup, tracking, NewProgress(), Ended)!.Result);
    }

    [Fact]
    public void Without_both_dispositions_there_is_no_game_to_record()
    {
        var provider = ScoringFixtures.Provider();
        var setup = new BattleSetupState(provider);
        var tracking = new ScoreTrackingState(setup, provider);
        setup.SelectYourDisposition(MissionArchetype.TakeAndHold);

        Assert.Null(ScorecardFactory.Create(setup, tracking, NewProgress(), Ended));
    }

    [Fact]
    public void Optional_setup_choices_stay_empty_when_the_wizard_was_not_finished()
    {
        var (setup, tracking) = ScoringFixtures.Prepared();

        var card = ScorecardFactory.Create(setup, tracking, NewProgress(), Ended)!;

        Assert.Null(card.Layout);
        Assert.Null(card.Role);
        Assert.Null(card.FirstTurn);
    }

    [Fact]
    public async Task The_first_turn_stays_unset_while_play_is_still_in_deployment()
    {
        var (setup, tracking) = ScoringFixtures.Prepared();
        var progress = NewProgress();
        await progress.SetFirstTurnAsync(Side.Enemy);

        Assert.Null(ScorecardFactory.Create(setup, tracking, progress, Ended)!.FirstTurn);
    }
}

public class ScorecardStoreTests
{
    private static Scorecard Card(DateTimeOffset endedAt, int youTotal = 10) => new(
        Guid.NewGuid(),
        endedAt,
        MissionArchetype.Disruption,
        MissionArchetype.Reconnaissance,
        "Smoke and Mirrors",
        "Surveil the Foe",
        "Dawn of War",
        BattleRole.Attacker,
        Side.You,
        SideCard(youTotal),
        SideCard(3),
        GameResult.Victory);

    private static SideScorecard SideCard(int total) => new([1, 2, 3, 4, 5], 6, total, SecondaryMode.Fixed, [0, 0, 0, 0, 0], 0, 0, 0, total);

    [Fact]
    public async Task Scorecards_come_back_newest_first()
    {
        var store = new ScorecardStore(new InMemoryRulesDataStore());
        var old = Card(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var recent = Card(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));

        await store.SaveAsync(recent);
        await store.SaveAsync(old);

        Assert.Equal([recent.Id, old.Id], (await store.ListAsync()).Select(s => s.Id));
    }

    [Fact]
    public async Task Saving_the_same_id_again_replaces_the_scorecard()
    {
        var store = new ScorecardStore(new InMemoryRulesDataStore());
        var card = Card(DateTimeOffset.UnixEpoch);

        await store.SaveAsync(card);
        await store.SaveAsync(card with { YourMission = "Changed" });

        var list = await store.ListAsync();
        Assert.Equal("Changed", Assert.Single(list).YourMission);
    }

    [Fact]
    public async Task Deleting_removes_only_that_scorecard()
    {
        var store = new ScorecardStore(new InMemoryRulesDataStore());
        var keep = Card(DateTimeOffset.UnixEpoch);
        var drop = Card(DateTimeOffset.UnixEpoch.AddDays(1));
        await store.SaveAsync(keep);
        await store.SaveAsync(drop);

        await store.DeleteAsync(drop.Id);

        Assert.Equal([keep.Id], (await store.ListAsync()).Select(s => s.Id));
    }

    [Fact]
    public async Task An_empty_store_lists_nothing()
    {
        Assert.Empty(await new ScorecardStore(new InMemoryRulesDataStore()).ListAsync());
    }

    [Fact]
    public async Task A_scorecard_written_as_json_reads_back_identically_which_is_what_survives_a_reload()
    {
        var jsonStore = new JsonRoundTripStore();
        var card = Card(new DateTimeOffset(2026, 10, 9, 12, 30, 0, TimeSpan.Zero), youTotal: 42);
        await new ScorecardStore(jsonStore).SaveAsync(card);

        var reloaded = Assert.Single(await new ScorecardStore(jsonStore).ListAsync());

        Assert.Equal(card.Id, reloaded.Id);
        Assert.Equal(card.EndedAtUtc, reloaded.EndedAtUtc);
        Assert.Equal((card.YourDisposition, card.OpponentDisposition, card.Role, card.FirstTurn, card.Result), (reloaded.YourDisposition, reloaded.OpponentDisposition, reloaded.Role, reloaded.FirstTurn, reloaded.Result));
        Assert.Equal((card.YourMission, card.OpponentMission, card.Layout), (reloaded.YourMission, reloaded.OpponentMission, reloaded.Layout));
        Assert.Equal(card.You.PrimaryRounds, reloaded.You.PrimaryRounds);
        Assert.Equal((42, SecondaryMode.Fixed), (reloaded.You.Total, reloaded.You.SecondaryMode));
        Assert.Contains("\"Disruption\"", jsonStore.Json.Values.Single(), StringComparison.Ordinal);
    }

    /// <summary>Stores every value as the JSON string the IndexedDB store would write, so only what survives serialization survives a "reload".</summary>
    private sealed class JsonRoundTripStore : IRulesDataStore
    {
        public Dictionary<string, string> Json { get; } = [];

        public Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            Json[key] = JsonSerializer.Serialize(value, MergedDataJson.Options);
            return Task.CompletedTask;
        }

        public Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Json.TryGetValue(key, out var json) ? JsonSerializer.Deserialize<T>(json, MergedDataJson.Options) : default);
    }
}

public class GameRecorderTests
{
    private static BattleProgressState NewProgress() => new(new InMemoryRulesDataStore());

    private static (GameRecorder Recorder, FakeScorecardStore Store, BattleSetupState Setup, ScoreTrackingState Tracking) Create()
    {
        var (setup, tracking) = ScoringFixtures.Prepared();
        var store = new FakeScorecardStore();
        return (new GameRecorder(setup, tracking, NewProgress(), store, TimeProvider.System), store, setup, tracking);
    }

    [Fact]
    public async Task Ending_the_game_saves_the_scorecard_and_starts_from_a_clean_slate()
    {
        var (recorder, store, setup, tracking) = Create();
        tracking.AdjustPrimary(Side.You, ScoringFixtures.CountableIndex, 1);
        var saved = 0;
        recorder.Saved += () => saved++;

        var card = await recorder.EndGameAsync();

        Assert.Same(card, Assert.Single(store.Scorecards));
        Assert.Same(card, recorder.LastEnded);
        Assert.Equal(GameResult.Victory, card!.Result);
        Assert.Equal(1, saved);
        Assert.Null(setup.YourDisposition);
        Assert.Null(setup.ResolvedMission);
        Assert.Equal(0, tracking.ScoreOf(Side.You).Total);
        Assert.Equal(1, tracking.CurrentRound);
    }

    [Fact]
    public async Task Without_a_game_nothing_is_saved()
    {
        var provider = ScoringFixtures.Provider();
        var setup = new BattleSetupState(provider);
        var store = new FakeScorecardStore();
        var recorder = new GameRecorder(setup, new ScoreTrackingState(setup, provider), NewProgress(), store, TimeProvider.System);

        Assert.Null(await recorder.EndGameAsync());
        Assert.Empty(store.Scorecards);
        Assert.Null(recorder.LastEnded);
    }

    [Fact]
    public async Task A_failing_save_leaves_the_game_untouched()
    {
        var (recorder, store, setup, tracking) = Create();
        tracking.AdjustPrimary(Side.You, ScoringFixtures.CountableIndex, 1);
        store.FailSavesWith = new InvalidOperationException("disk full");
        var saved = 0;
        recorder.Saved += () => saved++;

        await Assert.ThrowsAsync<InvalidOperationException>(() => recorder.EndGameAsync());

        Assert.Equal(0, saved);
        Assert.Null(recorder.LastEnded);
        Assert.NotNull(setup.YourDisposition);
        Assert.Equal(5, tracking.ScoreOf(Side.You).Total);
    }

    [Fact]
    public async Task The_recorded_time_comes_from_the_time_provider()
    {
        var (_, store, setup, tracking) = Create();
        var clock = new FixedTime(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero));

        await new GameRecorder(setup, tracking, NewProgress(), store, clock).EndGameAsync();

        Assert.Equal(clock.Now, Assert.Single(store.Scorecards).EndedAtUtc);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
