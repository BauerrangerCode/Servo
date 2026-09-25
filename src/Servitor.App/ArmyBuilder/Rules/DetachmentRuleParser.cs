using System.Text.Json;
using Servitor.App.ArmyBuilder.Catalog;

namespace Servitor.App.ArmyBuilder.Rules;

/// <summary>
/// Reads the detachment rules file. A rule that cannot be read (unknown type, missing field) is left out and named in
/// the warnings, so one broken entry does not take the other rules down with it.
/// </summary>
/// <remarks>
/// The file is a JSON object with a "rules" array. Every rule has "faction", "detachment", "type", "label" and, except
/// where noted, "units" (a selector: names, keywords, allKeywords, roles, excludeNames, excludeKeywords, outsideFaction). The types
/// are "mustInclude" (+ "min"), "cannotInclude", "pointsCap" (+ "caps": points by battle size name) and
/// "unitLimit" (+ "max"). An optional "note" is for the maintainers and is not read.
/// </remarks>
public static class DetachmentRuleParser
{
    private static readonly JsonDocumentOptions DocumentOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static DetachmentRuleSet Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, DocumentOptions);
        }
        catch (JsonException ex)
        {
            return new DetachmentRuleSet([], [$"The rules file is not valid JSON: {ex.Message}"]);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("rules", out var rules)
                || rules.ValueKind != JsonValueKind.Array)
            {
                return new DetachmentRuleSet([], ["The rules file has no \"rules\" array."]);
            }

            var parsed = new List<DetachmentRule>();
            var warnings = new List<string>();
            var index = 0;
            foreach (var entry in rules.EnumerateArray())
            {
                index++;
                var (rule, problem) = ParseRule(entry);
                if (rule is not null)
                {
                    parsed.Add(rule);
                }
                else
                {
                    warnings.Add($"Rule {index}{Describe(entry)}: {problem}");
                }
            }

            return new DetachmentRuleSet(parsed, warnings);
        }
    }

    private static (DetachmentRule? Rule, string? Problem) ParseRule(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            return (null, "is not an object.");
        }

        foreach (var field in new[] { "faction", "detachment", "type", "label" })
        {
            if (Text(entry, field) is null)
            {
                return (null, $"has no \"{field}\".");
            }
        }

        var faction = Text(entry, "faction")!;
        var detachment = Text(entry, "detachment")!;
        var label = Text(entry, "label")!;
        var type = Text(entry, "type")!;

        var (selector, selectorProblem) = ParseSelector(entry);
        if (selector is null)
        {
            return (null, selectorProblem);
        }

        switch (type)
        {
            case "mustInclude":
                return Number(entry, "min") is { } min and > 0
                    ? (new MustIncludeRule(faction, detachment, label, selector, min), null)
                    : (null, "needs a \"min\" of at least 1.");
            case "cannotInclude":
                return (new CannotIncludeRule(faction, detachment, label, selector), null);
            case "unitLimit":
                return Number(entry, "max") is { } max and > 0
                    ? (new UnitLimitOverrideRule(faction, detachment, label, selector, max), null)
                    : (null, "needs a \"max\" of at least 1.");
            case "pointsCap":
                return ParseCaps(entry) is { Count: > 0 } caps
                    ? (new PointsCapRule(faction, detachment, label, selector, caps), null)
                    : (null, "needs \"caps\": points by battle size name.");
            default:
                return (null, $"has the unknown type \"{type}\".");
        }
    }

    private static (UnitSelector? Selector, string? Problem) ParseSelector(JsonElement entry)
    {
        if (!entry.TryGetProperty("units", out var units))
        {
            return (UnitSelector.Any, null);
        }

        if (units.ValueKind != JsonValueKind.Object)
        {
            return (null, "has \"units\" that is not an object.");
        }

        var roles = new List<UnitRole>();
        foreach (var name in Strings(units, "roles"))
        {
            if (!Enum.TryParse<UnitRole>(name, ignoreCase: true, out var role))
            {
                return (null, $"names the unknown role \"{name}\".");
            }

            roles.Add(role);
        }

        bool? outside = units.TryGetProperty("outsideFaction", out var flag) && flag.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? flag.GetBoolean()
            : null;

        return (new UnitSelector(
            Strings(units, "names"),
            Strings(units, "keywords"),
            Strings(units, "allKeywords"),
            roles,
            Strings(units, "excludeNames"),
            Strings(units, "excludeKeywords"),
            outside), null);
    }

    private static Dictionary<string, int> ParseCaps(JsonElement entry)
    {
        var caps = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (entry.TryGetProperty("caps", out var element) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var points) && points >= 0)
                {
                    caps[property.Name] = points;
                }
            }
        }

        return caps;
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

    private static string Describe(JsonElement entry) =>
        entry.ValueKind == JsonValueKind.Object && Text(entry, "detachment") is { } detachment ? $" ({detachment})" : "";
}
