using Servitor.App.RulesData.Storage;
using Servitor.App.Scoring.Tracking;

namespace Servitor.App.BattleTracker.State;

public sealed class BattleProgressState(IRulesDataStore store) : IBattleProgressState
{
    public const int MinRound = 1;
    public const int MaxRound = ScoringRules.BattleRounds;

    private const string Key = "battle/progress";
    private const BattlePhase CommandPhase = BattlePhase.Command;

    private bool _isLoaded;
    private CommandPointLedger _commandPoints = CommandPointLedger.Empty;

    public event Action? Changed;

    public int Round { get; private set; } = MinRound;

    public BattlePhase Phase { get; private set; } = BattlePhase.Deployment;

    public Side FirstTurn { get; private set; } = Side.You;

    public Side ActiveTurn { get; private set; } = Side.You;

    public int CommandPointsOf(Side side) => _commandPoints.Of(side);

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;
        if (await store.LoadAsync<BattleProgress>(Key, cancellationToken) is { } stored
            && stored.Round is >= MinRound and <= MaxRound
            && Enum.IsDefined(stored.Phase)
            && Enum.IsDefined(stored.FirstTurn)
            && Enum.IsDefined(stored.ActiveTurn)
            && (stored.CommandPoints?.IsValid ?? true))
        {
            _commandPoints = stored.CommandPoints ?? CommandPointLedger.Empty;
            (Round, Phase, FirstTurn, ActiveTurn) = (stored.Round, stored.Phase, stored.FirstTurn, stored.ActiveTurn);
            Changed?.Invoke();
        }
    }

    public Task NextPhaseAsync()
    {
        if (Phase != BattlePhase.EndOfTurn)
        {
            return SetPhaseAsync(Phase + 1);
        }

        if (ActiveTurn == FirstTurn)
        {
            return UpdateAsync(Round, CommandPhase, FirstTurn, Other(FirstTurn));
        }

        return Round < MaxRound ? UpdateAsync(Round + 1, CommandPhase, FirstTurn, FirstTurn) : Task.CompletedTask;
    }

    public Task PreviousPhaseAsync()
    {
        if (Phase != CommandPhase)
        {
            return Phase == BattlePhase.Deployment ? Task.CompletedTask : SetPhaseAsync(Phase - 1);
        }

        if (ActiveTurn != FirstTurn)
        {
            return UpdateAsync(Round, BattlePhase.EndOfTurn, FirstTurn, FirstTurn);
        }

        return Round > MinRound
            ? UpdateAsync(Round - 1, BattlePhase.EndOfTurn, FirstTurn, Other(FirstTurn))
            : SetPhaseAsync(BattlePhase.Deployment);
    }

    public Task SetPhaseAsync(BattlePhase phase) => UpdateAsync(Round, phase, FirstTurn, ActiveTurn);

    public Task SetFirstTurnAsync(Side side) =>
        Phase == BattlePhase.Deployment ? UpdateAsync(Round, Phase, side, side) : Task.CompletedTask;

    public Task IncrementRoundAsync() => UpdateAsync(Round + 1, Phase, FirstTurn, ActiveTurn);

    public Task DecrementRoundAsync() => UpdateAsync(Round - 1, Phase, FirstTurn, ActiveTurn);

    public Task AdjustCommandPointsAsync(Side side, int delta) => ApplyAsync(Round, Phase, FirstTurn, ActiveTurn, _commandPoints.Adjust(side, delta));

    public async Task<bool> SpendCommandPointsAsync(int cost)
    {
        if (cost < 0 || !_commandPoints.CanSpend(Side.You, cost))
        {
            return false;
        }

        await ApplyAsync(Round, Phase, FirstTurn, ActiveTurn, _commandPoints.Spend(Side.You, cost));
        return true;
    }

    private static Side Other(Side side) => side == Side.You ? Side.Enemy : Side.You;

    private async Task UpdateAsync(int round, BattlePhase phase, Side firstTurn, Side activeTurn)
    {
        if (round is < MinRound or > MaxRound || !Enum.IsDefined(phase))
        {
            return;
        }

        var enteringCommandPhase = phase == CommandPhase && Phase != CommandPhase;
        var commandPoints = enteringCommandPhase ? _commandPoints.GrantForCommandPhase(round, activeTurn) : _commandPoints;
        await ApplyAsync(round, phase, firstTurn, activeTurn, commandPoints);
    }

    private async Task ApplyAsync(int round, BattlePhase phase, Side firstTurn, Side activeTurn, CommandPointLedger commandPoints)
    {
        (Round, Phase, FirstTurn, ActiveTurn, _commandPoints) = (round, phase, firstTurn, activeTurn, commandPoints);
        Changed?.Invoke();
        await store.SaveAsync(Key, new BattleProgress(round, phase, firstTurn, activeTurn, commandPoints));
    }
}
