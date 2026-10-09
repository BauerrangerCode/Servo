using System.Text.Json;
using Servitor.RulesData.Domain;

namespace Servitor.App.Scoring.MissionDeck;

/// <summary>
/// Reads the Mission Deck file. An entry that cannot be read (unknown disposition, missing field) is left out and
/// named in the warnings, so one broken entry does not take the rest of the deck down with it.
/// </summary>
/// <remarks>
/// The file is a JSON object with a "primaryMissions" array (each entry: "yourDisposition", "opponentDisposition"
/// — <see cref="MissionArchetype"/> names —, "name", "legend", "scoringConditions" — each with "roundScope", "when",
/// "condition", "points" and an optional "relation" ("Or"/"Cumulative", default standalone) — and an optional
/// "additionalRules" array of verbatim rule-text blocks) and a "deploymentCards" array ("name", "attackerZone",
/// "defenderZone", "objectives").
/// </remarks>
public static class MissionDeckParser
{
    private static readonly JsonDocumentOptions DocumentOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static MissionDeckSet Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, DocumentOptions);
        }
        catch (JsonException ex)
        {
            return new MissionDeckSet(MissionDeckData.Empty, [$"The Mission Deck file is not valid JSON: {ex.Message}"]);
        }

        using (document)
        {
            var warnings = new List<string>();
            var missions = ParseMissions(document.RootElement, warnings);
            var cards = ParseDeploymentCards(document.RootElement, warnings);
            return new MissionDeckSet(new MissionDeckData(missions, cards), warnings);
        }
    }

    private static Dictionary<(MissionArchetype, MissionArchetype), PrimaryMission> ParseMissions(JsonElement root, List<string> warnings)
    {
        var missions = new Dictionary<(MissionArchetype, MissionArchetype), PrimaryMission>();
        if (!root.TryGetProperty("primaryMissions", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            warnings.Add("The Mission Deck file has no \"primaryMissions\" array.");
            return missions;
        }

        var index = 0;
        foreach (var entry in array.EnumerateArray())
        {
            index++;
            var (key, mission, problem) = ParseMission(entry);
            if (mission is not null)
            {
                missions[key] = mission;
            }
            else
            {
                warnings.Add($"Primary mission {index}: {problem}");
            }
        }

        return missions;
    }

    private static ((MissionArchetype, MissionArchetype) Key, PrimaryMission? Mission, string? Problem) ParseMission(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            return (default, null, "is not an object.");
        }

        if (Text(entry, "yourDisposition") is not { } yoursText || !Enum.TryParse<MissionArchetype>(yoursText, out var yours))
        {
            return (default, null, "has a missing or unknown \"yourDisposition\".");
        }

        if (Text(entry, "opponentDisposition") is not { } opponentText || !Enum.TryParse<MissionArchetype>(opponentText, out var opponent))
        {
            return (default, null, "has a missing or unknown \"opponentDisposition\".");
        }

        if (Text(entry, "name") is not { } name)
        {
            return (default, null, "has no \"name\".");
        }

        var legend = Text(entry, "legend") ?? "";
        var conditions = ParseScoringConditions(entry);
        var additionalRules = Strings(entry, "additionalRules");

        return ((yours, opponent), new PrimaryMission(name, legend, conditions, additionalRules), null);
    }

    private static List<ScoringCondition> ParseScoringConditions(JsonElement entry)
    {
        var conditions = new List<ScoringCondition>();
        if (!entry.TryGetProperty("scoringConditions", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return conditions;
        }

        foreach (var condition in array.EnumerateArray())
        {
            if (condition.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var roundScope = Text(condition, "roundScope") ?? "";
            var when = Text(condition, "when") ?? "";
            var text = Text(condition, "condition");
            var points = Number(condition, "points");
            if (text is null || points is null)
            {
                continue;
            }

            var relation = Text(condition, "relation") is { } relationText && Enum.TryParse<ScoringRelation>(relationText, out var parsed)
                ? parsed
                : ScoringRelation.Standalone;

            conditions.Add(new ScoringCondition(roundScope, when, text, points.Value, relation));
        }

        return conditions;
    }

    private static List<DeploymentCard> ParseDeploymentCards(JsonElement root, List<string> warnings)
    {
        var cards = new List<DeploymentCard>();
        if (!root.TryGetProperty("deploymentCards", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            warnings.Add("The Mission Deck file has no \"deploymentCards\" array.");
            return cards;
        }

        var index = 0;
        foreach (var entry in array.EnumerateArray())
        {
            index++;
            if (entry.ValueKind != JsonValueKind.Object || Text(entry, "name") is not { } name)
            {
                warnings.Add($"Deployment card {index} has no \"name\".");
                continue;
            }

            cards.Add(new DeploymentCard(name, Text(entry, "attackerZone") ?? "", Text(entry, "defenderZone") ?? "", Text(entry, "objectives") ?? ""));
        }

        return cards;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;

    private static int? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;

    private static List<string> Strings(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!).ToList()
            : [];
}
