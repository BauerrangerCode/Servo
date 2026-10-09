using Servitor.App.Scoring.MissionDeck;
using Servitor.App.Scoring.State;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.Scoring.State;

internal sealed class FakeMissionDeckProvider(MissionDeckSet? deck = null) : IMissionDeckProvider
{
    public MissionDeckSet Current { get; set; } = deck ?? MissionDeckSet.Empty;

    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class BattleSetupStateTests
{
    private static readonly PrimaryMission Mission = new("Immovable Object", "Hold.", [new ScoringCondition("Any Battle Round", "End of your turn.", "Control an objective.", 3)], []);
    private static readonly DeploymentCard Card = new("Hammer and Anvil", "Top edge.", "Bottom edge.", "One central objective.");

    private static BattleSetupState PreparedState() => new(new FakeMissionDeckProvider(new MissionDeckSet(
        new MissionDeckData(new Dictionary<(MissionArchetype, MissionArchetype), PrimaryMission> { [(MissionArchetype.TakeAndHold, MissionArchetype.PurgeTheFoe)] = Mission }, [Card]),
        [])));

    [Fact]
    public void Both_dispositions_resolve_the_mission_they_map_to()
    {
        var state = PreparedState();

        Assert.Null(state.ResolvedMission);
        state.SelectYourDisposition(MissionArchetype.TakeAndHold);
        Assert.Null(state.ResolvedMission);
        state.SelectOpponentDisposition(MissionArchetype.PurgeTheFoe);

        Assert.Same(Mission, state.ResolvedMission);
    }

    [Fact]
    public void An_unmapped_pair_resolves_to_no_mission()
    {
        var state = PreparedState();
        state.SelectYourDisposition(MissionArchetype.Disruption);
        state.SelectOpponentDisposition(MissionArchetype.Reconnaissance);

        Assert.Null(state.ResolvedMission);
    }

    [Fact]
    public void Selecting_a_deployment_card_by_name_resolves_the_full_card()
    {
        var state = PreparedState();

        state.SelectDeploymentCard("Hammer and Anvil");

        Assert.Same(Card, state.SelectedDeploymentCard);
    }

    [Fact]
    public void Role_is_a_manual_choice()
    {
        var state = PreparedState();

        state.SelectRole(BattleRole.Defender);

        Assert.Equal(BattleRole.Defender, state.Role);
    }

    [Fact]
    public void Reset_clears_every_choice()
    {
        var state = PreparedState();
        state.SelectYourDisposition(MissionArchetype.TakeAndHold);
        state.SelectOpponentDisposition(MissionArchetype.PurgeTheFoe);
        state.SelectDeploymentCard("Hammer and Anvil");
        state.SelectRole(BattleRole.Attacker);

        state.Reset();

        Assert.Null(state.YourDisposition);
        Assert.Null(state.OpponentDisposition);
        Assert.Null(state.ResolvedMission);
        Assert.Null(state.SelectedDeploymentCard);
        Assert.Null(state.Role);
    }

    [Fact]
    public void Changed_fires_on_every_selection()
    {
        var state = PreparedState();
        var raised = 0;
        state.Changed += () => raised++;

        state.SelectYourDisposition(MissionArchetype.TakeAndHold);
        state.SelectOpponentDisposition(MissionArchetype.PurgeTheFoe);
        state.SelectDeploymentCard("Hammer and Anvil");
        state.SelectRole(BattleRole.Attacker);
        state.Reset();

        Assert.Equal(5, raised);
    }

    [Fact]
    public void A_state_without_a_deck_provider_has_no_deployment_cards_and_resolves_nothing()
    {
        var state = new BattleSetupState();
        state.SelectYourDisposition(MissionArchetype.TakeAndHold);
        state.SelectOpponentDisposition(MissionArchetype.PurgeTheFoe);

        Assert.Empty(state.DeploymentCards);
        Assert.Null(state.ResolvedMission);
    }
}
