using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Servitor.DataUpdate.Wahapedia;

public static partial class DepotDataGenerator
{
    // Wahapedia separates a weapon from its profile with a hyphen or an en/em dash: "Plasma pistol – supercharge".
    [GeneratedRegex(@"\s[-–—]\s")]
    private static partial Regex ProfileSeparatorRegex();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex NumericCostRegex();

    // ---- detachments -------------------------------------------------------------------------------------------

    private static JsonArray BuildDetachments(WahapediaTables data, string factionId, Func<string, string> createSlug)
    {
        var seenNames = new HashSet<string>();
        var built = new List<JsonObject>();

        foreach (var row in data.Detachments.Where(d => d.Get("factionId") == factionId))
        {
            var name = row.Get("name");
            var id = row.Get("id");
            var slugSource = seenNames.Contains(name) ? $"{name}-{ShortIdSuffix(id)}" : name;
            seenNames.Add(name);

            built.Add(new JsonObject
            {
                ["id"] = id,
                ["slug"] = createSlug(slugSource),
                ["name"] = name,
                ["legend"] = row.Get("legend"),
                ["type"] = row.Get("type"),
                ["dp"] = row.Get("dp"),
                ["forceDisposition"] = row.Get("forceDisposition"),
                ["chapterDp"] = new JsonArray(data.DetachmentChapterDp
                    .Where(c => c.Get("detachmentId") == id)
                    .Select(c => (JsonNode?)new JsonObject { ["keyword"] = c.Get("keyword"), ["dp"] = c.Get("dp") })
                    .ToArray()),
                ["abilities"] = new JsonArray(SortByName(data.DetachmentAbilities
                        .Where(a => a.Get("factionId") == factionId && a.Get("detachmentId") == id)
                        .Select(ToDetachmentAbility))
                    .ToArray<JsonNode?>()),
                ["enhancements"] = new JsonArray(SortByName(data.Enhancements
                        .Where(e => e.Get("factionId") == factionId && e.Get("detachmentId") == id)
                        .Select(ToEnhancement))
                    .ToArray<JsonNode?>()),
                ["stratagems"] = new JsonArray(SortByName(data.Stratagems
                        .Where(s => s.Get("factionId") == factionId && s.Get("detachmentId") == id)
                        .Select(ToStratagem))
                    .ToArray<JsonNode?>()),
            });
        }

        return new JsonArray(SortByName(built).ToArray<JsonNode?>());
    }

    private static string ShortIdSuffix(string id)
    {
        var trimmed = id.TrimStart('0');
        var suffix = trimmed.Length > 4 ? trimmed[^4..] : trimmed;
        return suffix.Length > 0 ? suffix : (id.Length > 4 ? id[^4..] : id);
    }

    private static JsonObject ToEnhancement(CsvRow row)
    {
        var json = new JsonObject
        {
            ["id"] = row.Get("id"),
            ["factionId"] = row.Get("factionId"),
            ["name"] = row.Get("name"),
            ["legend"] = row.Get("legend"),
            ["description"] = row.Get("description"),
            ["cost"] = row.Get("cost"),
            ["detachment"] = row.Get("detachment"),
        };
        if (row.Get("upgrade") == "true")
        {
            json["upgrade"] = true;
        }

        if (row.Get("supportLeader").Length > 0)
        {
            json["supportLeader"] = row.Get("supportLeader");
        }

        return json;
    }

    private static JsonObject ToStratagem(CsvRow row) => new()
    {
        ["id"] = row.Get("id"),
        ["factionId"] = row.Get("factionId"),
        ["name"] = row.Get("name"),
        ["type"] = row.Get("type"),
        ["cpCost"] = row.Get("cpCost"),
        ["legend"] = row.Get("legend"),
        ["turn"] = row.Get("turn"),
        ["phase"] = row.Get("phase"),
        ["description"] = row.Get("description"),
        ["detachment"] = row.Get("detachment"),
    };

    private static JsonObject ToDetachmentAbility(CsvRow row) => new()
    {
        ["id"] = row.Get("id"),
        ["factionId"] = row.Get("factionId"),
        ["name"] = row.Get("name"),
        ["legend"] = row.Get("legend"),
        ["description"] = row.Get("description"),
        ["detachment"] = row.Get("detachment"),
    };

    // ---- wargear -----------------------------------------------------------------------------------------------

    private static (string BaseName, string? ProfileName) SplitWargearName(string name)
    {
        var trimmed = name.Trim();
        var match = ProfileSeparatorRegex().Match(trimmed);
        if (!match.Success)
        {
            return (trimmed, null);
        }

        var baseName = trimmed[..match.Index].Trim();
        var profileName = trimmed[(match.Index + match.Length)..].Trim();
        return baseName.Length > 0 && profileName.Length > 0 ? (baseName, profileName) : (trimmed, null);
    }

    /// <summary>Groups flattened wargear profiles into weapons with profiles ("Bolt rifle" with "standard" and "frag").</summary>
    private static JsonArray GroupWargear(List<CsvRow> entries)
    {
        var slug = Slugs.CreateGenerator("wargear");
        var groups = new List<(JsonObject Group, JsonArray Profiles)>();

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var (baseName, profileName) = SplitWargearName(entry.Get("name"));
            var normalizedBaseName = baseName.Length > 0 ? baseName : (entry.Get("name").Length > 0 ? entry.Get("name") : $"Wargear {index + 1}");

            var lineInWargear = int.TryParse(entry.Get("lineInWargear").Length > 0 ? entry.Get("lineInWargear") : "1", out var parsed) ? parsed : 1;
            var current = groups.Count > 0 ? groups[^1] : default;
            var continueGroup = groups.Count > 0
                && entry.Get("datasheetId") == current.Group["datasheetId"]!.GetValue<string>()
                && normalizedBaseName == current.Group["name"]!.GetValue<string>()
                && lineInWargear > 1;

            if (!continueGroup)
            {
                var line = OrDefault(entry.Get("line"), groups.Count + 1);
                var group = new JsonObject
                {
                    ["id"] = $"{entry.Get("datasheetId")}:{slug(normalizedBaseName)}",
                    ["datasheetId"] = entry.Get("datasheetId"),
                    ["line"] = line,
                    ["name"] = normalizedBaseName,
                    ["type"] = entry.Get("type"),
                };
                groups.Add((group, new JsonArray()));
                current = groups[^1];
            }

            var profile = new JsonObject
            {
                ["datasheetId"] = entry.Get("datasheetId"),
                ["line"] = OrDefault(entry.Get("line"), current.Profiles.Count + 1),
                ["lineInWargear"] = entry.Get("lineInWargear").Length > 0 ? entry.Get("lineInWargear") : "1",
                ["dice"] = entry.Get("dice"),
                ["name"] = entry.Get("name"),
            };
            if (profileName is not null)
            {
                profile["profileName"] = profileName;
            }

            profile["description"] = entry.Get("description");
            profile["range"] = entry.Get("range");
            profile["type"] = entry.Get("type");
            profile["a"] = entry.Get("a");
            profile["bsWs"] = entry.Get("bsWs");
            profile["s"] = entry.Get("s");
            profile["ap"] = entry.Get("ap");
            profile["d"] = entry.Get("d");
            current.Profiles.Add(profile);
        }

        var result = new JsonArray();
        for (var index = 0; index < groups.Count; index++)
        {
            var (group, profiles) = groups[index];
            var types = profiles.Select(p => p!["type"]!.GetValue<string>()).Distinct().ToList();
            group["type"] = types.Count == 1 ? types[0] : "Mixed";
            var firstLine = profiles.Count > 0 ? profiles[0]!["line"]!.GetValue<string>() : "";
            group["line"] = firstLine.Length > 0 ? firstLine : (group["line"]!.GetValue<string>().Length > 0 ? group["line"]!.GetValue<string>() : $"{index + 1}");
            group["profiles"] = profiles;
            result.Add(group);
        }

        return result;
    }

    private static string OrDefault(string value, int fallback) => value.Trim().Length > 0 ? value : $"{fallback}";

    // ---- model costs -------------------------------------------------------------------------------------------

    private static bool HasNumericCost(string cost) => NumericCostRegex().IsMatch(cost.Trim());

    private static string StripTags(string value) =>
        Regex.Replace(TagRegex().Replace(value, " "), @"\s+", " ").Trim();

    /// <summary>
    /// Drops empty-cost header rows, stamps the preceding header onto the real cost rows as "section", and collapses
    /// exact (description, cost, section) duplicates.
    /// </summary>
    private static JsonArray NormalizeModelCosts(IEnumerable<CsvRow> rows)
    {
        string? section = null;
        var seen = new HashSet<string>();
        var costs = new JsonArray();

        foreach (var row in rows)
        {
            if (!HasNumericCost(row.Get("cost")))
            {
                var header = StripTags(row.Get("description"));
                if (header.Length > 0)
                {
                    section = header;
                }

                continue;
            }

            var cost = row.Get("cost").Trim();
            if (!seen.Add($"{row.Get("description")}\0{cost}\0{section ?? ""}"))
            {
                continue;
            }

            var json = new JsonObject
            {
                ["datasheetId"] = row.Get("datasheetId"),
                ["line"] = row.Get("line"),
                ["description"] = row.Get("description"),
                ["cost"] = cost,
            };
            if (!string.IsNullOrEmpty(section))
            {
                json["section"] = section;
            }

            costs.Add(json);
        }

        return costs;
    }

    /// <summary>The cheapest cost, suffixed with "+" when the datasheet has more than one price; null when there is none.</summary>
    private static string? SummarizeCosts(JsonArray costs)
    {
        var values = costs.Select(c => int.Parse(c!["cost"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture)).ToList();
        if (values.Count == 0)
        {
            return null;
        }

        var cheapest = values.Min();
        return values.Distinct().Count() > 1 ? $"{cheapest}+" : $"{cheapest}";
    }

    /// <summary>Orders names the way the source does (JavaScript <c>localeCompare</c>): culture-aware, case-insensitive first.</summary>
    private sealed class NameComparer : IComparer<string>
    {
        public static readonly NameComparer Instance = new();

        public int Compare(string? x, string? y) => string.Compare(x, y, StringComparison.InvariantCulture);
    }
}
