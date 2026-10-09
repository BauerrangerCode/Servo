using Servitor.App.Scoring.MissionDeck;
using Servitor.App.Scoring.State;
using Servitor.App.Scoring.Tracking;
using Servitor.App.Tests.Scoring.State;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.Scoring;

/// <summary>A tiny Mission Deck: Take and Hold vs Purge the Foe for you, the swapped pair for the enemy, and four Secondary cards.</summary>
internal static class ScoringFixtures
{
    public const int CountableIndex = 0;
    public const int YesNoIndex = 1;
    public const int EndOfBattleIndex = 2;
    public const int SecondRoundOnwardsIndex = 3;

    public static readonly PrimaryMission YourMission = new(
        "Immovable Object",
        "Hold.",
        [
            new ScoringCondition("Any Battle Round", "End of your turn.", "For each objective you control.", 5),
            new ScoringCondition("Any Battle Round", "End of your turn.", "You control more objectives than your opponent.", 2),
            new ScoringCondition("End of the Battle", "", "You control your opponent's home objective.", 8, ScoringRelation.Standalone, 5, 5, true),
            new ScoringCondition("Second Battle Round Onwards", "End of your turn.", "You control a central objective.", 3, ScoringRelation.Standalone, 2, 5),
        ],
        []);

    public static readonly PrimaryMission EnemyMission = new(
        "Unstoppable Force",
        "Smash.",
        [new ScoringCondition("Any Battle Round", "End of your turn.", "For each enemy unit destroyed.", 4)],
        []);

    public static readonly SecondaryMission Alpha = Card("Alpha", fixedEligible: true, new SecondaryCondition("Any Battle Round", "End of a turn.", "For each enemy unit destroyed.", 4, 5));
    public static readonly SecondaryMission Beta = Card("Beta", fixedEligible: true, new SecondaryCondition("Any Battle Round", "End of your turn.", "You hold the centre.", 3, 3));
    public static readonly SecondaryMission Gamma = Card("Gamma", fixedEligible: true, new SecondaryCondition("Any Battle Round", "End of your turn.", "You hold a corner.", 2, 2));
    public static readonly SecondaryMission Delta = Card(
        "Delta",
        fixedEligible: false,
        new SecondaryCondition("Any Battle Round", "End of a turn.", "For each model destroyed.", 3, null),
        new SecondaryCondition("Any Battle Round", "End of a turn.", "A character was destroyed.", null, 5),
        new SecondaryCondition("Second Battle Round Onwards", "End of a turn.", "You hold your home.", 2, 2, ScoringRelation.Standalone, 2, 5));

    public static readonly DeploymentCard Layout = new("Hammer and Anvil", "Top edge.", "Bottom edge.", "One central objective.");

    public static MissionDeckData Deck() => new(
        new Dictionary<(MissionArchetype, MissionArchetype), PrimaryMission>
        {
            [(MissionArchetype.TakeAndHold, MissionArchetype.PurgeTheFoe)] = YourMission,
            [(MissionArchetype.PurgeTheFoe, MissionArchetype.TakeAndHold)] = EnemyMission,
        },
        [Layout])
    {
        SecondaryMissions = [Alpha, Beta, Gamma, Delta],
    };

    public static FakeMissionDeckProvider Provider() => new(new MissionDeckSet(Deck(), []));

    public static (BattleSetupState Setup, ScoreTrackingState Tracking) Prepared(int seed = 1)
    {
        var provider = Provider();
        var setup = new BattleSetupState(provider);
        setup.SelectYourDisposition(MissionArchetype.TakeAndHold);
        setup.SelectOpponentDisposition(MissionArchetype.PurgeTheFoe);
        return (setup, new ScoreTrackingState(setup, provider, new Random(seed)));
    }

    private static SecondaryMission Card(string name, bool fixedEligible, params SecondaryCondition[] conditions) =>
        new(name, "", "", [], fixedEligible, null, conditions);
}
