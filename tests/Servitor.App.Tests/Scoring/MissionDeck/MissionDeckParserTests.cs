using Servitor.App.Scoring.MissionDeck;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.Scoring.MissionDeck;

public class MissionDeckParserTests
{
    private const string OneMissionAndCard = """
        {
          "primaryMissions": [
            {
              "yourDisposition": "TakeAndHold",
              "opponentDisposition": "PurgeTheFoe",
              "name": "Immovable Object",
              "legend": "Hold the line.",
              "scoringConditions": [
                { "roundScope": "Any Battle Round", "when": "End of your turn.", "condition": "You control one or more central objectives.", "points": 3 },
                { "roundScope": "End of the Battle", "when": "", "condition": "You control your opponent's home objective.", "points": 5, "relation": "Cumulative" }
              ],
              "additionalRules": ["Some setup rule."]
            }
          ],
          "deploymentCards": [
            { "name": "Hammer and Anvil", "attackerZone": "Top edge.", "defenderZone": "Bottom edge.", "objectives": "One central objective." }
          ]
        }
        """;

    [Fact]
    public void A_mission_and_a_deployment_card_are_read_without_warnings()
    {
        var set = MissionDeckParser.Parse(OneMissionAndCard);

        Assert.Empty(set.Warnings);
        var mission = set.Data.Resolve(MissionArchetype.TakeAndHold, MissionArchetype.PurgeTheFoe);
        Assert.NotNull(mission);
        Assert.Equal("Immovable Object", mission!.Name);
        Assert.Equal("Hold the line.", mission.Legend);
        Assert.Equal(["Some setup rule."], mission.AdditionalRules);

        Assert.Equal(2, mission.ScoringConditions.Count);
        Assert.Equal(ScoringRelation.Standalone, mission.ScoringConditions[0].Relation);
        Assert.Equal(3, mission.ScoringConditions[0].Points);
        Assert.Equal(ScoringRelation.Cumulative, mission.ScoringConditions[1].Relation);
        Assert.Equal("", mission.ScoringConditions[1].When);

        var card = Assert.Single(set.Data.DeploymentCards);
        Assert.Equal("Hammer and Anvil", card.Name);
        Assert.Equal("Top edge.", card.AttackerZone);
    }

    [Fact]
    public void An_unresolved_pair_returns_null()
    {
        var set = MissionDeckParser.Parse(OneMissionAndCard);

        Assert.Null(set.Data.Resolve(MissionArchetype.Disruption, MissionArchetype.Reconnaissance));
    }

    [Fact]
    public void A_mission_with_an_unknown_disposition_is_skipped_with_a_warning()
    {
        var set = MissionDeckParser.Parse("""
            { "primaryMissions": [ { "yourDisposition": "NotADisposition", "opponentDisposition": "TakeAndHold", "name": "X", "scoringConditions": [] } ], "deploymentCards": [] }
            """);

        Assert.Empty(set.Data.PrimaryMissions);
        Assert.Contains(set.Warnings, w => w.Contains("yourDisposition", StringComparison.Ordinal));
    }

    [Fact]
    public void A_deployment_card_without_a_name_is_skipped_with_a_warning()
    {
        var set = MissionDeckParser.Parse("""
            { "primaryMissions": [], "deploymentCards": [ { "attackerZone": "x" } ] }
            """);

        Assert.Empty(set.Data.DeploymentCards);
        Assert.Contains(set.Warnings, w => w.Contains("Deployment card 1", StringComparison.Ordinal));
    }

    [Fact]
    public void Invalid_json_produces_a_single_warning_and_an_empty_deck()
    {
        var set = MissionDeckParser.Parse("not json");

        Assert.Same(MissionDeckData.Empty.PrimaryMissions, set.Data.PrimaryMissions);
        Assert.Empty(set.Data.PrimaryMissions);
        Assert.Single(set.Warnings);
    }

    // ---- the shipped Mission Deck file ------------------------------------------------------------------------

    private static string DeckFile => TestPaths.WwwrootDataFile("missions", "mission-deck.json");

    [Fact]
    public void The_shipped_deck_file_is_read_without_warnings()
    {
        var set = MissionDeckParser.Parse(File.ReadAllText(DeckFile));

        Assert.Empty(set.Warnings);
    }

    [Fact]
    public void The_shipped_deck_resolves_all_25_disposition_pairs_to_a_distinct_mission()
    {
        var set = MissionDeckParser.Parse(File.ReadAllText(DeckFile));
        var archetypes = Enum.GetValues<MissionArchetype>();

        var resolved = new List<string>();
        foreach (var yours in archetypes)
        {
            foreach (var opponent in archetypes)
            {
                var mission = set.Data.Resolve(yours, opponent);
                Assert.True(mission is not null, $"{yours} vs {opponent} has no resolved mission.");
                resolved.Add(mission!.Name);
            }
        }

        Assert.Equal(25, resolved.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Every_scoring_condition_of_the_shipped_deck_has_a_positive_point_value()
    {
        var set = MissionDeckParser.Parse(File.ReadAllText(DeckFile));

        Assert.All(
            set.Data.PrimaryMissions.Values.SelectMany(m => m.ScoringConditions),
            condition => Assert.True(condition.Points > 0, $"'{condition.Condition}' has a non-positive point value."));
    }

    [Fact]
    public void The_shipped_deck_has_exactly_the_6_named_deployment_cards()
    {
        var set = MissionDeckParser.Parse(File.ReadAllText(DeckFile));

        Assert.Equal(
            new[] { "Hammer and Anvil", "Dawn of War", "Search and Destroy", "Crucible of Battle", "Tipping Point", "Sweeping Engagement" },
            set.Data.DeploymentCards.Select(c => c.Name));
    }
}
