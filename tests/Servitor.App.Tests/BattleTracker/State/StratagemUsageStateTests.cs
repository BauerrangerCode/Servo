using Servitor.App.BattleTracker.State;
using Servitor.App.Scoring.Tracking;
using Servitor.App.Tests.RulesData.Loading;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.BattleTracker.State;

public class StratagemUsageStateTests
{
    private static readonly Guid UnitA = Guid.NewGuid();
    private static readonly Guid UnitB = Guid.NewGuid();
    private static readonly Stratagem Grenade = BattleTrackerFixtures.Stratagem("g", "GRENADE", "Shooting phase");
    private static readonly Stratagem Smoke = BattleTrackerFixtures.Stratagem("s", "SMOKESCREEN", "Shooting phase");

    private readonly InMemoryRulesDataStore _store = new();
    private readonly BattleProgressState _progress;
    private readonly StratagemUsageState _usage;

    public StratagemUsageStateTests()
    {
        _progress = new BattleProgressState(_store);
        _usage = new StratagemUsageState(_store, _progress);
    }

    private async Task GoToShootingWithCpAsync(int cp)
    {
        await _progress.AdjustCommandPointsAsync(Side.You, cp);
        await _progress.SetPhaseAsync(BattlePhase.Shooting);
    }

    [Fact]
    public async Task Using_a_stratagem_pays_its_cost_and_records_the_use()
    {
        await GoToShootingWithCpAsync(3);

        var outcome = await _usage.UseAsync(Grenade, UnitA);

        Assert.True(outcome.IsUsed);
        Assert.Equal(2, _progress.CommandPointsOf(Side.You));
        Assert.True(_usage.IsUsedThisPhase("g"));
    }

    [Fact]
    public async Task The_same_stratagem_cannot_be_used_twice_in_one_phase_and_the_second_try_costs_nothing()
    {
        await GoToShootingWithCpAsync(3);
        await _usage.UseAsync(Grenade, UnitA);

        var outcome = await _usage.UseAsync(Grenade, UnitB);

        Assert.Equal(StratagemUseResult.AlreadyUsedThisPhase, outcome.Result);
        Assert.Equal(2, _progress.CommandPointsOf(Side.You));
    }

    [Fact]
    public async Task A_unit_cannot_be_targeted_by_a_second_stratagem_in_the_same_phase()
    {
        await GoToShootingWithCpAsync(3);
        await _usage.UseAsync(Grenade, UnitA);

        var outcome = await _usage.UseAsync(Smoke, UnitA);

        Assert.Equal(StratagemUseResult.TargetAlreadyTargeted, outcome.Result);
        Assert.Contains("GRENADE", outcome.Reason);
        Assert.False(_usage.IsUsedThisPhase("s"));
        Assert.Equal(2, _progress.CommandPointsOf(Side.You));
    }

    [Fact]
    public async Task A_different_unit_or_no_unit_is_not_blocked()
    {
        await GoToShootingWithCpAsync(3);
        await _usage.UseAsync(Grenade, UnitA);

        Assert.True((await _usage.UseAsync(Smoke, UnitB)).IsUsed);
    }

    [Fact]
    public async Task Allowing_a_retarget_lets_a_stratagem_that_says_so_hit_an_already_targeted_unit()
    {
        await GoToShootingWithCpAsync(3);
        await _usage.UseAsync(Grenade, UnitA);

        var outcome = await _usage.UseAsync(Smoke, UnitA, allowRetarget: true);

        Assert.True(outcome.IsUsed);
    }

    [Fact]
    public async Task Without_enough_command_points_nothing_is_recorded()
    {
        await GoToShootingWithCpAsync(0);

        var outcome = await _usage.UseAsync(Grenade, UnitA);

        Assert.Equal(StratagemUseResult.NotEnoughCommandPoints, outcome.Result);
        Assert.False(_usage.IsUsedThisPhase("g"));
    }

    [Fact]
    public async Task The_limits_start_over_in_the_next_phase_and_in_the_same_phase_of_the_other_turn()
    {
        await GoToShootingWithCpAsync(5);
        await _usage.UseAsync(Grenade, UnitA);

        await _progress.SetPhaseAsync(BattlePhase.Fight);
        Assert.False(_usage.IsUsedThisPhase("g"));
        Assert.Null(_usage.TargetingThisPhase(UnitA));

        await _progress.SetPhaseAsync(BattlePhase.EndOfTurn);
        await _progress.NextPhaseAsync();
        await _progress.SetPhaseAsync(BattlePhase.Shooting);
        Assert.Equal(Side.Enemy, _progress.ActiveTurn);
        Assert.False(_usage.IsUsedThisPhase("g"));
    }

    [Fact]
    public async Task Stepping_back_into_a_phase_finds_its_uses_again()
    {
        await GoToShootingWithCpAsync(3);
        await _usage.UseAsync(Grenade, UnitA);
        await _progress.SetPhaseAsync(BattlePhase.Charge);

        await _progress.SetPhaseAsync(BattlePhase.Shooting);

        Assert.True(_usage.IsUsedThisPhase("g"));
    }

    [Fact]
    public async Task Undo_takes_the_use_back_and_refunds_the_command_points()
    {
        await GoToShootingWithCpAsync(3);
        await _usage.UseAsync(Grenade, UnitA);

        await _usage.UndoAsync("g");

        Assert.False(_usage.IsUsedThisPhase("g"));
        Assert.Equal(3, _progress.CommandPointsOf(Side.You));
        Assert.Null(_usage.TargetingThisPhase(UnitA));
    }

    [Fact]
    public async Task Uses_survive_a_reload()
    {
        await GoToShootingWithCpAsync(3);
        await _usage.UseAsync(Grenade, UnitA);

        var progress = new BattleProgressState(_store);
        await progress.LoadAsync();
        var usage = new StratagemUsageState(_store, progress);
        await usage.LoadAsync();

        Assert.True(usage.IsUsedThisPhase("g"));
        Assert.NotNull(usage.TargetingThisPhase(UnitA));
    }
}
