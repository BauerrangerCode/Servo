using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.Scoring.MissionDeck;
using Servitor.App.Scoring.State;
using Servitor.App.Scoring.Tracking;
using Servitor.App.Tests.Scoring.State;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.Scoring.Pages;

public class ScoringTests : BunitContext
{
    private static readonly PrimaryMission Mission = new(
        "Immovable Object",
        "Hold the line.",
        [
            new ScoringCondition("Any Battle Round", "End of your turn.", "You control one or more central objectives.", 3),
            new ScoringCondition("End of the Battle", "", "You control your opponent's home objective.", 5, ScoringRelation.Cumulative),
        ],
        ["Some setup rule."]);

    private static readonly DeploymentCard Card = new("Hammer and Anvil", "Top edge zone.", "Bottom edge zone.", "One central objective.");

    private readonly BattleSetupState _state;

    public ScoringTests()
    {
        var deck = new MissionDeckData(
            new Dictionary<(MissionArchetype, MissionArchetype), PrimaryMission> { [(MissionArchetype.TakeAndHold, MissionArchetype.PurgeTheFoe)] = Mission },
            [Card]);
        _state = new BattleSetupState(new FakeMissionDeckProvider(new MissionDeckSet(deck, [])));

        var provider = new FakeMissionDeckProvider(new MissionDeckSet(deck, []));

        Services.AddSingleton<IBattleSetupState>(_state);
        Services.AddSingleton<IMissionDeckProvider>(provider);
        Services.AddSingleton<IScoreTrackingState>(new ScoreTrackingState(_state, provider));
    }

    // Several disposition chips share the same label between the "Your Disposition" and "Opponent Disposition"
    // sections, so a button must be looked up within its own <section>, not by label alone.
    private static void ClickInSection(IRenderedComponent<Servitor.App.Scoring.Pages.Scoring> cut, int sectionIndex, string buttonText) =>
        cut.FindAll("section.setup-step")[sectionIndex].QuerySelectorAll("button").First(b => b.TextContent.Trim() == buttonText).Click();

    private static void SelectBothDispositions(IRenderedComponent<Servitor.App.Scoring.Pages.Scoring> cut)
    {
        ClickInSection(cut, 0, "Take and Hold");
        ClickInSection(cut, 1, "Purge the Foe");
    }

    [Fact]
    public void Opponent_disposition_only_appears_after_your_own_is_picked()
    {
        var cut = Render<Servitor.App.Scoring.Pages.Scoring>();

        Assert.DoesNotContain("Opponent Disposition", cut.Markup);

        ClickInSection(cut, 0, "Take and Hold");

        Assert.Contains("Opponent Disposition", cut.Markup);
    }

    [Fact]
    public void Picking_both_dispositions_resolves_and_shows_the_mission()
    {
        var cut = Render<Servitor.App.Scoring.Pages.Scoring>();

        SelectBothDispositions(cut);

        Assert.Contains("Immovable Object", cut.Markup);
        Assert.Contains("Hold the line.", cut.Markup);
        Assert.Contains("You control one or more central objectives.", cut.Markup);
        Assert.Contains("3VP", cut.Markup);
        Assert.Contains("+5VP", cut.Markup);
        Assert.Contains("Some setup rule.", cut.Markup);
    }

    [Fact]
    public void Selecting_a_deployment_layout_shows_its_zones_and_objectives()
    {
        var cut = Render<Servitor.App.Scoring.Pages.Scoring>();
        SelectBothDispositions(cut);

        cut.Find("select.deployment-select").Change("Hammer and Anvil");

        Assert.Contains("Top edge zone.", cut.Markup);
        Assert.Contains("Bottom edge zone.", cut.Markup);
    }

    [Fact]
    public void Setup_is_only_marked_complete_once_role_and_first_turn_are_both_chosen()
    {
        var cut = Render<Servitor.App.Scoring.Pages.Scoring>();
        SelectBothDispositions(cut);

        Assert.DoesNotContain("Setup complete", cut.Markup);

        ClickInSection(cut, 4, "You are the Defender");
        Assert.DoesNotContain("Setup complete", cut.Markup);

        ClickInSection(cut, 5, "Enemy");
        Assert.Contains("Setup complete", cut.Markup);
    }

    [Fact]
    public void Reset_returns_to_the_disposition_picker_only()
    {
        var cut = Render<Servitor.App.Scoring.Pages.Scoring>();
        SelectBothDispositions(cut);
        Assert.Contains("Immovable Object", cut.Markup);

        cut.Find("button.reset-setup").Click();

        Assert.DoesNotContain("Immovable Object", cut.Markup);
        Assert.DoesNotContain("Opponent Disposition", cut.Markup);
    }
}
