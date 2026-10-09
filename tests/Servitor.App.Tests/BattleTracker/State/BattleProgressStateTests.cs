using Servitor.App.BattleTracker.State;
using Servitor.App.Scoring.Tracking;
using Servitor.App.Tests.RulesData.Loading;

namespace Servitor.App.Tests.BattleTracker.State;

public class BattleProgressStateTests
{
    private readonly InMemoryRulesDataStore _store = new();

    private BattleProgressState NewState() => new(_store);

    [Fact]
    public void StartsInDeploymentOfRoundOne()
    {
        var state = NewState();

        Assert.Equal(BattlePhase.Deployment, state.Phase);
        Assert.Equal(1, state.Round);
    }

    [Fact]
    public async Task NextWalksTheFirstTurnThenSwitchesToTheOtherPlayerInTheSameRound()
    {
        var state = NewState();
        BattlePhase[] firstTurn =
        [
            BattlePhase.Command, BattlePhase.Movement, BattlePhase.Shooting, BattlePhase.Charge,
            BattlePhase.Fight, BattlePhase.EndOfTurn,
        ];

        foreach (var phase in firstTurn)
        {
            await state.NextPhaseAsync();
            Assert.Equal(phase, state.Phase);
            Assert.Equal(Side.You, state.ActiveTurn);
        }

        await state.NextPhaseAsync();

        Assert.Equal((BattlePhase.Command, Side.Enemy, 1), (state.Phase, state.ActiveTurn, state.Round));
    }

    [Fact]
    public async Task TheRoundAdvancesOnlyWhenTheSecondTurnEnds()
    {
        var state = NewState();
        await EndTurnsAsync(state, 2);

        Assert.Equal((BattlePhase.Command, Side.You, 2), (state.Phase, state.ActiveTurn, state.Round));
    }

    [Fact]
    public async Task TheEnemyCanTakeTheFirstTurnOfEveryRound()
    {
        var state = NewState();
        await state.SetFirstTurnAsync(Side.Enemy);
        Assert.Equal(Side.Enemy, state.ActiveTurn);

        await EndTurnsAsync(state, 1);
        Assert.Equal((Side.You, 1), (state.ActiveTurn, state.Round));

        await EndTurnsAsync(state, 1);
        Assert.Equal((Side.Enemy, 2), (state.ActiveTurn, state.Round));
    }

    [Fact]
    public async Task FirstTurnCanOnlyBeChosenDuringDeployment()
    {
        var state = NewState();
        await state.NextPhaseAsync();

        await state.SetFirstTurnAsync(Side.Enemy);

        Assert.Equal(Side.You, state.FirstTurn);
    }

    [Fact]
    public async Task NextDoesNothingAfterTheLastTurnOfTheLastRound()
    {
        var state = NewState();
        await EndTurnsAsync(state, BattleProgressState.MaxRound * 2 - 1);
        await state.SetPhaseAsync(BattlePhase.EndOfTurn);
        Assert.Equal((BattleProgressState.MaxRound, Side.Enemy), (state.Round, state.ActiveTurn));

        await state.NextPhaseAsync();

        Assert.Equal((BattleProgressState.MaxRound, BattlePhase.EndOfTurn, Side.Enemy), (state.Round, state.Phase, state.ActiveTurn));
    }

    [Fact]
    public async Task PreviousUndoesNextAcrossTurnsRoundsAndDeployment()
    {
        var state = NewState();
        var steps = new List<(int, BattlePhase, Side)>();
        for (var i = 0; i < 20; i++)
        {
            steps.Add((state.Round, state.Phase, state.ActiveTurn));
            await state.NextPhaseAsync();
        }

        for (var i = steps.Count - 1; i >= 0; i--)
        {
            await state.PreviousPhaseAsync();
            Assert.Equal(steps[i], (state.Round, state.Phase, state.ActiveTurn));
        }
    }

    [Fact]
    public async Task PreviousInDeploymentDoesNothing()
    {
        var state = NewState();

        await state.PreviousPhaseAsync();

        Assert.Equal(BattlePhase.Deployment, state.Phase);
        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public async Task RoundChangesIndependentlyOfThePhaseAndStaysWithinTheBattleRounds()
    {
        var state = NewState();
        await state.SetPhaseAsync(BattlePhase.Fight);

        await state.DecrementRoundAsync();
        Assert.Equal(BattleProgressState.MinRound, state.Round);

        for (var i = 0; i < BattleProgressState.MaxRound + 2; i++)
        {
            await state.IncrementRoundAsync();
        }

        Assert.Equal(BattleProgressState.MaxRound, state.Round);
        Assert.Equal(BattlePhase.Fight, state.Phase);
    }

    [Fact]
    public async Task EveryChangeRaisesChanged()
    {
        var state = NewState();
        var raised = 0;
        state.Changed += () => raised++;

        await state.NextPhaseAsync();
        await state.IncrementRoundAsync();

        Assert.Equal(2, raised);
    }

    [Fact]
    public async Task ProgressSurvivesAReload()
    {
        var before = NewState();
        await before.SetPhaseAsync(BattlePhase.Charge);
        await before.IncrementRoundAsync();
        await before.IncrementRoundAsync();
        await before.SetPhaseAsync(BattlePhase.EndOfTurn);
        await before.NextPhaseAsync();

        var after = NewState();
        await after.LoadAsync();

        Assert.Equal((3, BattlePhase.Command, Side.Enemy), (after.Round, after.Phase, after.ActiveTurn));
    }

    [Fact]
    public async Task StoredProgressOutsideTheValidRangeIsIgnored()
    {
        _store.Values["battle/progress"] = new BattleProgress(99, (BattlePhase)42, Side.You, Side.You);
        var state = NewState();

        await state.LoadAsync();

        Assert.Equal(1, state.Round);
        Assert.Equal(BattlePhase.Deployment, state.Phase);
    }

    private static async Task EndTurnsAsync(BattleProgressState state, int turns)
    {
        for (var i = 0; i < turns; i++)
        {
            await state.SetPhaseAsync(BattlePhase.EndOfTurn);
            await state.NextPhaseAsync();
        }
    }
}
