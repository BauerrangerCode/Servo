using Servitor.App.BattleTracker.Opponent;
using Servitor.App.BattleTracker.State;
using Servitor.App.RulesData.Loading;
using Servitor.App.Scoring.State;
using Servitor.App.Scoring.Tracking;
using Servitor.App.Tests.RulesData.Loading;
using Servitor.App.Tests.Scoring;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.BattleTracker.State;

public class SharedRoundTests
{
    private readonly InMemoryRulesDataStore _store = new();
    private readonly BattleProgressState _progress;
    private readonly ScoreTrackingState _tracking;

    public SharedRoundTests()
    {
        _progress = new BattleProgressState(_store);
        var provider = ScoringFixtures.Provider();
        var setup = new BattleSetupState(provider);
        _tracking = new ScoreTrackingState(setup, provider, _progress);
    }

    [Fact]
    public async Task Scoring_reads_the_battle_round_of_play()
    {
        await _progress.IncrementRoundAsync();
        await _progress.IncrementRoundAsync();

        Assert.Equal(3, _tracking.CurrentRound);
    }

    [Fact]
    public async Task Setting_the_round_in_scoring_sets_it_in_play()
    {
        await _tracking.SetRoundAsync(4);

        Assert.Equal(4, _progress.Round);
    }

    [Fact]
    public async Task Finishing_the_second_turn_of_a_round_moves_scoring_to_the_next_round()
    {
        await _progress.SetPhaseAsync(BattlePhase.EndOfTurn);
        await _progress.NextPhaseAsync();
        await _progress.SetPhaseAsync(BattlePhase.EndOfTurn);
        await _progress.NextPhaseAsync();

        Assert.Equal(2, _tracking.CurrentRound);
    }

    [Fact]
    public async Task A_round_outside_the_battle_is_ignored()
    {
        await _tracking.SetRoundAsync(6);
        await _tracking.SetRoundAsync(0);

        Assert.Equal(1, _progress.Round);
    }

    [Fact]
    public async Task A_change_of_the_round_in_play_notifies_scoring_listeners()
    {
        var raised = 0;
        _tracking.Changed += () => raised++;

        await _progress.IncrementRoundAsync();

        Assert.Equal(1, raised);
    }
}

public class BattlePlaySessionTests
{
    [Fact]
    public async Task Resetting_wipes_progress_command_points_uses_rule_choices_and_the_opponent()
    {
        var store = new InMemoryRulesDataStore();
        var progress = new BattleProgressState(store);
        var usage = new StratagemUsageState(store, progress);
        var rules = new ArmyRuleState(store);
        var provider = new BattleTrackerFixtures.FakeRulesDataProvider(null);
        var opponent = new OpponentArmyState(provider, new FakeDatasheetCatalog([]));
        var grenade = BattleTrackerFixtures.Stratagem("g", "GRENADE", "Shooting phase");

        await progress.SetFirstTurnAsync(Side.Enemy);
        await progress.NextPhaseAsync();
        await progress.IncrementRoundAsync();
        await progress.SetPhaseAsync(BattlePhase.Shooting);
        await usage.UseAsync(grenade, null);
        await rules.PinAsync("r");
        await rules.SetShowAurasAsync(false);

        await new BattlePlaySession(progress, usage, rules, opponent).ResetAsync();

        Assert.Equal((1, BattlePhase.Deployment, Side.You, Side.You), (progress.Round, progress.Phase, progress.FirstTurn, progress.ActiveTurn));
        Assert.Equal(0, progress.CommandPointsOf(Side.You));
        Assert.False(usage.IsUsedThisPhase("g"));
        Assert.False(rules.IsPinned("r"));
        Assert.False(rules.ShowAuras);

        var reloaded = new BattleProgressState(store);
        await reloaded.LoadAsync();
        Assert.Equal((1, BattlePhase.Deployment), (reloaded.Round, reloaded.Phase));
    }
}
