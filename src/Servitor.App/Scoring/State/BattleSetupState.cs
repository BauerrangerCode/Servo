using Servitor.App.Scoring.MissionDeck;
using Servitor.RulesData.Domain;

namespace Servitor.App.Scoring.State;

public sealed class BattleSetupState(IMissionDeckProvider? missionDeckProvider = null) : IBattleSetupState
{
    public event Action? Changed;

    public MissionArchetype? YourDisposition { get; private set; }

    public MissionArchetype? OpponentDisposition { get; private set; }

    public string? DeploymentCardName { get; private set; }

    public BattleRole? Role { get; private set; }

    public FirstTurn? FirstTurn { get; private set; }

    private MissionDeckData Deck => missionDeckProvider?.Current.Data ?? MissionDeckData.Empty;

    public IReadOnlyList<DeploymentCard> DeploymentCards => Deck.DeploymentCards;

    public DeploymentCard? SelectedDeploymentCard => DeploymentCards.FirstOrDefault(c => c.Name == DeploymentCardName);

    public PrimaryMission? ResolvedMission =>
        YourDisposition is { } yours && OpponentDisposition is { } opponent ? Deck.Resolve(yours, opponent) : null;

    public PrimaryMission? OpponentMission =>
        YourDisposition is { } yours && OpponentDisposition is { } opponent ? Deck.Resolve(opponent, yours) : null;

    public void SelectYourDisposition(MissionArchetype disposition)
    {
        YourDisposition = disposition;
        Changed?.Invoke();
    }

    public void SelectOpponentDisposition(MissionArchetype disposition)
    {
        OpponentDisposition = disposition;
        Changed?.Invoke();
    }

    public void SelectDeploymentCard(string name)
    {
        DeploymentCardName = name;
        Changed?.Invoke();
    }

    public void SelectRole(BattleRole role)
    {
        Role = role;
        Changed?.Invoke();
    }

    public void SelectFirstTurn(FirstTurn firstTurn)
    {
        FirstTurn = firstTurn;
        Changed?.Invoke();
    }

    public void Reset()
    {
        YourDisposition = null;
        OpponentDisposition = null;
        DeploymentCardName = null;
        Role = null;
        FirstTurn = null;
        Changed?.Invoke();
    }
}
