using Servitor.App.Scoring.MissionDeck;
using Servitor.RulesData.Domain;

namespace Servitor.App.Scoring.State;

/// <summary>
/// The mission setup wizard's progress and result (E5.1). Lives in a scoped service, not in components, so a later
/// VP-tracking screen (E5.2) and Play can read the resolved mission without redoing the wizard.
/// </summary>
public interface IBattleSetupState
{
    event Action? Changed;

    MissionArchetype? YourDisposition { get; }

    MissionArchetype? OpponentDisposition { get; }

    /// <summary>The Primary Mission resolved from both dispositions; null until both are picked.</summary>
    PrimaryMission? ResolvedMission { get; }

    /// <summary>The Mission Deck's 6 named Deployment cards, for the layout picker.</summary>
    IReadOnlyList<DeploymentCard> DeploymentCards { get; }

    string? DeploymentCardName { get; }

    DeploymentCard? SelectedDeploymentCard { get; }

    BattleRole? Role { get; }

    FirstTurn? FirstTurn { get; }

    void SelectYourDisposition(MissionArchetype disposition);

    void SelectOpponentDisposition(MissionArchetype disposition);

    void SelectDeploymentCard(string name);

    void SelectRole(BattleRole role);

    void SelectFirstTurn(FirstTurn firstTurn);

    /// <summary>Discards the whole setup in progress, so a new game can be started from a clean slate.</summary>
    void Reset();
}
