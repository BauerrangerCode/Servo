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
          ],
          "secondaryMissions": []
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

    [Fact]
    public void Secondary_missions_keep_their_per_mode_values_and_rounds()
    {
        var set = MissionDeckParser.Parse("""
            {
              "primaryMissions": [], "deploymentCards": [],
              "secondaryMissions": [
                {
                  "name": "Assassination", "legend": "Hunt.", "fixedEligible": true, "turnCap": 5, "whenDrawn": "Pick one.",
                  "additionalRules": ["Note."],
                  "conditions": [
                    { "roundScope": "Any Battle Round", "when": "End of a turn.", "condition": "For each model.", "fixedPoints": 3 },
                    { "roundScope": "Second Battle Round Onwards", "when": "End of a turn.", "condition": "Bonus.", "tacticalPoints": 5, "relation": "Cumulative", "firstRound": 2, "lastRound": 5 },
                    { "condition": "Has no value in any mode." }
                  ]
                }
              ]
            }
            """);

        Assert.Empty(set.Warnings);
        var card = Assert.Single(set.Data.SecondaryMissions);
        Assert.True(card.FixedEligible);
        Assert.Equal(5, card.TurnCap);
        Assert.Equal("Pick one.", card.WhenDrawn);
        Assert.Equal(["Note."], card.AdditionalRules);
        Assert.Equal(2, card.Conditions.Count);
        Assert.Equal(3, card.Conditions[0].PointsIn(SecondaryMode.Fixed));
        Assert.Null(card.Conditions[0].PointsIn(SecondaryMode.Tactical));
        Assert.Equal(5, card.Conditions[1].PointsIn(SecondaryMode.Tactical));
        Assert.Equal((2, 5), (card.Conditions[1].FirstRound, card.Conditions[1].LastRound));
        Assert.Equal(ScoringRelation.Cumulative, card.Conditions[1].Relation);
        Assert.Same(card, set.Data.FindSecondary("Assassination"));
    }

    [Fact]
    public void A_secondary_mission_without_a_readable_condition_is_skipped_with_a_warning()
    {
        var set = MissionDeckParser.Parse("""
            { "primaryMissions": [], "deploymentCards": [], "secondaryMissions": [ { "name": "Empty", "conditions": [] } ] }
            """);

        Assert.Empty(set.Data.SecondaryMissions);
        Assert.Contains(set.Warnings, w => w.Contains("Empty", StringComparison.Ordinal));
    }

    [Fact]
    public void Primary_conditions_read_their_round_range_and_end_of_battle_flag()
    {
        var set = MissionDeckParser.Parse("""
            {
              "primaryMissions": [ { "yourDisposition": "TakeAndHold", "opponentDisposition": "TakeAndHold", "name": "X", "scoringConditions": [
                { "roundScope": "Second Battle Round Onwards", "when": "w", "condition": "c", "points": 2, "firstRound": 2, "lastRound": 5 },
                { "roundScope": "End of the Battle", "when": "", "condition": "c", "points": 5, "firstRound": 5, "lastRound": 5, "endOfBattle": true },
                { "roundScope": "Any Battle Round", "when": "w", "condition": "c", "points": 1 }
              ] } ],
              "deploymentCards": [], "secondaryMissions": []
            }
            """);

        var conditions = set.Data.Resolve(MissionArchetype.TakeAndHold, MissionArchetype.TakeAndHold)!.ScoringConditions;
        Assert.Equal((2, 5, false), (conditions[0].FirstRound, conditions[0].LastRound, conditions[0].IsEndOfBattle));
        Assert.Equal((5, 5, true), (conditions[1].FirstRound, conditions[1].LastRound, conditions[1].IsEndOfBattle));
        Assert.Equal((1, 5, false), (conditions[2].FirstRound, conditions[2].LastRound, conditions[2].IsEndOfBattle));
    }

    // ---- the shipped Mission Deck file ------------------------------------------------------------------------

    [Fact]
    public void The_shipped_deck_has_the_18_secondary_missions_and_exactly_4_fixed_eligible_ones()
    {
        var deck = MissionDeckParser.Parse(File.ReadAllText(DeckFile)).Data;

        Assert.Equal(18, deck.SecondaryMissions.Count);
        Assert.Equal(
            ["A Grievous Blow", "Assassination", "Bring It Down", "Engage on All Fronts"],
            deck.SecondaryMissions.Where(m => m.FixedEligible).Select(m => m.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_shipped_secondary_condition_scores_in_at_least_one_mode_and_fixed_cards_differ_only_where_the_source_does()
    {
        var deck = MissionDeckParser.Parse(File.ReadAllText(DeckFile)).Data;

        Assert.All(
            deck.SecondaryMissions.SelectMany(m => m.Conditions),
            c => Assert.True(c.FixedPoints is not null || c.TacticalPoints is not null));
        Assert.All(
            deck.SecondaryMissions.Where(m => !m.FixedEligible).SelectMany(m => m.Conditions),
            c => Assert.Equal(c.FixedPoints, c.TacticalPoints));
        Assert.Equal(4, deck.FindSecondary("A Grievous Blow")!.Conditions[0].FixedPoints);
        Assert.Equal(5, deck.FindSecondary("A Grievous Blow")!.Conditions[0].TacticalPoints);
    }

    [Fact]
    public void Every_shipped_end_of_battle_condition_is_flagged_and_only_scoreable_in_the_last_round()
    {
        var deck = MissionDeckParser.Parse(File.ReadAllText(DeckFile)).Data;
        var conditions = deck.PrimaryMissions.Values.SelectMany(m => m.ScoringConditions).ToList();

        Assert.All(
            conditions.Where(c => c.RoundScope == "End of the Battle"),
            c => Assert.True(c.IsEndOfBattle && c.FirstRound == 5 && c.LastRound == 5));
        Assert.All(conditions.Where(c => c.IsEndOfBattle), c => Assert.Equal("End of the Battle", c.RoundScope));
        Assert.All(conditions, c => Assert.InRange(c.FirstRound, 1, c.LastRound));
    }


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
