using System.Text.Json.Nodes;

namespace Servitor.DataUpdate.Wahapedia;

/// <summary>One faction as written to <c>factions/&lt;slug&gt;/</c>: its manifest and one JSON document per datasheet.</summary>
public sealed record GeneratedFaction(string Slug, JsonObject Manifest, IReadOnlyList<(string DatasheetId, JsonObject Datasheet)> Datasheets);

public sealed record GeneratedData(IReadOnlyList<GeneratedFaction> Factions, JsonArray CoreStratagems, string? DataVersion);

/// <summary>
/// Turns the Wahapedia tables into the faction / datasheet / core-stratagem JSON documents the app's rules-text
/// mapper reads. A port of depot's <c>generate-data.ts</c> (and the <c>@depot/core</c> helpers it uses), so the
/// files keep exactly the shape they had when depot produced them. Property order follows depot's.
/// </summary>
public static partial class DepotDataGenerator
{
    private const string CodexKey = "codex";

    // Source ids are globally unique across factions (from depot's supplements config).
    private static readonly Dictionary<string, (string Slug, string Name)> Supplements = new()
    {
        ["000000139"] = ("codex", "Codex"),
        ["000000362"] = ("codex", "Codex"),
        ["000000356"] = ("codex", "Codex (Legends)"),
        ["000000021"] = ("blood-angels", "Blood Angels"),
        ["000000376"] = ("blood-angels", "Blood Angels (Legends)"),
        ["000000023"] = ("dark-angels", "Dark Angels"),
        ["000000373"] = ("dark-angels", "Dark Angels (Legends)"),
        ["000000036"] = ("space-wolves", "Space Wolves"),
        ["000000360"] = ("space-wolves", "Space Wolves (Legends)"),
        ["000000162"] = ("black-templars", "Black Templars"),
        ["000000035"] = ("deathwatch", "Deathwatch"),
        ["000000363"] = ("imperial-agents-legends", "Imperial Agents (Legends)"),
    };

    public static GeneratedData Generate(WahapediaTables data)
    {
        var factionSlugGenerator = Slugs.CreateGenerator("faction");
        var datasheetSlugGenerator = Slugs.CreateGenerator("datasheet");
        var factionSlugs = data.Factions.ToDictionary(f => f.Get("id"), f => factionSlugGenerator(f.Get("name")));
        var datasheetSlugs = new Dictionary<string, string>();
        foreach (var datasheet in data.Datasheets)
        {
            datasheetSlugs[datasheet.Get("id")] = datasheetSlugGenerator(datasheet.Get("name"));
        }

        var lookups = new Lookups(data);
        var factions = data.Factions
            .Select(faction => BuildFaction(data, lookups, faction, datasheetSlugs, factionSlugs))
            .OfType<GeneratedFaction>()
            .ToList();

        var coreStratagems = new JsonArray(SortByName(data.Stratagems
                .Where(s => s.Get("factionId").Trim().Length == 0)
                .Select(ToStratagem))
            .ToArray<JsonNode?>());

        return new GeneratedData(factions, coreStratagems, data.LastUpdate);
    }

    private sealed class Lookups(WahapediaTables data)
    {
        public ILookup<string, CsvRow> Abilities { get; } = data.DatasheetAbilities.ToLookup(r => r.Get("datasheetId"));
        public ILookup<string, CsvRow> Keywords { get; } = data.DatasheetKeywords.ToLookup(r => r.Get("datasheetId"));
        public ILookup<string, CsvRow> Models { get; } = data.DatasheetModels.ToLookup(r => r.Get("datasheetId"));
        public ILookup<string, CsvRow> Options { get; } = data.DatasheetOptions.ToLookup(r => r.Get("datasheetId"));
        public ILookup<string, CsvRow> Wargear { get; } = data.DatasheetWargear.ToLookup(r => r.Get("datasheetId"));
        public ILookup<string, CsvRow> UnitComposition { get; } = data.DatasheetUnitComposition.ToLookup(r => r.Get("datasheetId"));
        public ILookup<string, CsvRow> ModelCosts { get; } = data.DatasheetModelCosts.ToLookup(r => r.Get("datasheetId"));
        public ILookup<string, CsvRow> Stratagems { get; } = data.DatasheetStratagems.ToLookup(r => r.Get("datasheetId"));
        public ILookup<string, CsvRow> Leaders { get; } = data.DatasheetLeaders.ToLookup(r => r.Get("leaderId"));
        public Dictionary<string, CsvRow> AbilitiesById { get; } = FirstById(data.Abilities);
        public Dictionary<string, CsvRow> SourcesById { get; } = FirstById(data.Sources);

        private static Dictionary<string, CsvRow> FirstById(IEnumerable<CsvRow> rows)
        {
            var result = new Dictionary<string, CsvRow>();
            foreach (var row in rows)
            {
                result.TryAdd(row.Get("id"), row);
            }

            return result;
        }
    }

    private static GeneratedFaction? BuildFaction(
        WahapediaTables data,
        Lookups lookups,
        CsvRow faction,
        Dictionary<string, string> datasheetSlugs,
        Dictionary<string, string> factionSlugs)
    {
        var factionId = faction.Get("id");
        var factionSlug = factionSlugs[factionId];

        var datasheets = data.Datasheets
            .Where(d => d.Get("factionId") == factionId && d.Get("virtual") == "false")
            .Select(d => BuildDatasheet(lookups, d, datasheetSlugs, factionSlugs))
            .OrderBy(d => d.Name, NameComparer.Instance)
            .ToList();
        if (datasheets.Count == 0)
        {
            return null;
        }

        var detachments = BuildDetachments(data, factionId, Slugs.CreateGenerator($"{factionSlug}-detachment"));

        var manifest = new JsonObject
        {
            ["id"] = factionId,
            ["slug"] = factionSlug,
            ["name"] = faction.Get("name"),
            ["link"] = faction.Get("link"),
            ["datasheets"] = new JsonArray(datasheets.Select(d => (JsonNode?)ToSummary(d, factionSlug, factionId)).ToArray()),
            ["detachments"] = detachments,
        };
        if (data.LastUpdate is not null)
        {
            manifest["dataVersion"] = data.LastUpdate;
        }

        manifest["datasheetCount"] = datasheets.Count;
        manifest["detachmentCount"] = detachments.Count;

        return new GeneratedFaction(factionSlug, manifest, datasheets.Select(d => (d.Id, d.Json)).ToList());
    }

    private sealed record BuiltDatasheet(string Id, string Name, JsonObject Json, string Role, string? Points);

    private static BuiltDatasheet BuildDatasheet(
        Lookups lookups,
        CsvRow datasheet,
        Dictionary<string, string> datasheetSlugs,
        Dictionary<string, string> factionSlugs)
    {
        var id = datasheet.Get("id");

        var sourceId = datasheet.Get("sourceId");
        var hasSource = lookups.SourcesById.TryGetValue(sourceId, out var source);
        var sourceName = hasSource ? source!.Get("name").Trim() : "";
        var isForgeWorld = sourceName.EndsWith("(Forge World)", StringComparison.Ordinal);
        var isLegends = sourceName.EndsWith("(Warhammer Legends)", StringComparison.Ordinal)
            || sourceName.StartsWith("Legends:", StringComparison.Ordinal);

        var hasSupplement = Supplements.TryGetValue(sourceId, out var supplement);
        var supplementKey = hasSupplement ? supplement.Slug : CodexKey;
        var isSupplement = supplementKey != CodexKey;

        var keywords = lookups.Keywords[id].ToList();
        var modelCosts = NormalizeModelCosts(lookups.ModelCosts[id]);

        var json = new JsonObject
        {
            ["id"] = id,
            ["slug"] = datasheetSlugs[id],
            ["name"] = datasheet.Get("name"),
            ["factionId"] = datasheet.Get("factionId"),
            ["factionSlug"] = factionSlugs[datasheet.Get("factionId")],
            ["sourceId"] = sourceId,
        };
        if (hasSource)
        {
            json["sourceName"] = $"{source!.Get("type")}: {source.Get("name")}";
        }

        json["supplementKey"] = supplementKey;
        if (hasSupplement)
        {
            json["supplementSlug"] = supplement.Slug;
            json["supplementName"] = supplement.Name;
        }

        json["supplementLabel"] = isSupplement && hasSupplement ? supplement.Name : "None";
        json["isSupplement"] = isSupplement;
        json["legend"] = datasheet.Get("legend");
        json["isSupport"] = datasheet.Get("isSupport") == "true";
        json["loadout"] = datasheet.Get("loadout");
        json["transport"] = datasheet.Get("transport");
        json["virtual"] = datasheet.Get("virtual") == "true";
        json["leaderHead"] = datasheet.Get("leaderHead");
        json["leaderFooter"] = datasheet.Get("leaderFooter");
        json["damagedW"] = datasheet.Get("damagedW");
        json["damagedDescription"] = datasheet.Get("damagedDescription");
        json["link"] = datasheet.Get("link");
        json["abilities"] = BuildAbilities(lookups, id);
        json["keywords"] = ToArray(keywords);
        json["models"] = ToArray(lookups.Models[id]);
        json["options"] = ToArray(lookups.Options[id]);
        json["wargear"] = GroupWargear(lookups.Wargear[id].ToList());
        json["unitComposition"] = ToArray(lookups.UnitComposition[id]);
        json["modelCosts"] = modelCosts;
        json["stratagemIds"] = new JsonArray(lookups.Stratagems[id].Select(r => (JsonNode?)r.Get("stratagemId")).ToArray());
        json["leaders"] = new JsonArray(lookups.Leaders[id]
            .Select(r => (JsonNode?)new JsonObject
            {
                ["id"] = r.Get("attachedId"),
                ["slug"] = datasheetSlugs.GetValueOrDefault(r.Get("attachedId")) ?? "Unknown",
            })
            .ToArray());
        json["isForgeWorld"] = isForgeWorld;
        json["isLegends"] = isLegends;

        return new BuiltDatasheet(id, datasheet.Get("name"), json, BattlefieldRole(keywords), SummarizeCosts(modelCosts));
    }

    private static JsonArray BuildAbilities(Lookups lookups, string datasheetId)
    {
        var abilities = new JsonArray();
        foreach (var row in lookups.Abilities[datasheetId])
        {
            var abilityId = row.Get("abilityId");
            if (abilityId.Length > 0)
            {
                if (lookups.AbilitiesById.TryGetValue(abilityId, out var referenced))
                {
                    abilities.Add(ReferencedAbility(referenced, row));
                }
            }
            else if (row.Get("name").Length > 0)
            {
                // Inline abilities carry no id / legend / factionId of their own.
                abilities.Add(new JsonObject
                {
                    ["id"] = "",
                    ["name"] = row.Get("name"),
                    ["legend"] = "",
                    ["factionId"] = "",
                    ["description"] = row.Get("description"),
                    ["type"] = row.Get("type"),
                    ["parameter"] = row.Get("parameter"),
                });
            }
        }

        return abilities;
    }

    private static JsonObject ReferencedAbility(CsvRow referenced, CsvRow usage)
    {
        var ability = ToObject(referenced);
        ability["type"] = usage.Get("type");
        var parameter = usage.Get("parameter");
        if (parameter.Length > 0)
        {
            ability["parameter"] = parameter;
        }

        return ability;
    }

    private static JsonObject ToSummary(BuiltDatasheet datasheet, string factionSlug, string factionId)
    {
        var json = datasheet.Json;
        var summary = new JsonObject
        {
            ["id"] = datasheet.Id,
            ["slug"] = json["slug"]!.GetValue<string>(),
            ["name"] = datasheet.Name,
            ["factionId"] = factionId,
            ["factionSlug"] = factionSlug,
            ["isSupport"] = json["isSupport"]!.GetValue<bool>(),
            ["role"] = datasheet.Role,
            ["points"] = datasheet.Points,
            ["supplementKey"] = json["supplementKey"]!.GetValue<string>(),
            ["path"] = $"/data/factions/{factionSlug}/datasheets/{datasheet.Id}.json",
        };
        foreach (var optional in new[] { "supplementSlug", "supplementName" })
        {
            if (json[optional] is { } value)
            {
                summary[optional] = value.GetValue<string>();
            }
        }

        summary["supplementLabel"] = json["supplementLabel"]!.GetValue<string>();
        summary["isSupplement"] = json["isSupplement"]!.GetValue<bool>();
        summary["link"] = json["link"]!.GetValue<string>();
        summary["isForgeWorld"] = json["isForgeWorld"]!.GetValue<bool>();
        summary["isLegends"] = json["isLegends"]!.GetValue<bool>();
        return summary;
    }

    private static string BattlefieldRole(IEnumerable<CsvRow> keywords)
    {
        var names = keywords.Select(k => k.Get("keyword").ToLowerInvariant()).ToList();
        if (names.Any(k => k.Contains("epic hero", StringComparison.Ordinal)))
        {
            return "epic-hero";
        }

        if (names.Any(k => k.Contains("character", StringComparison.Ordinal)))
        {
            return "character";
        }

        return names.Any(k => k.Contains("battleline", StringComparison.Ordinal)) ? "battleline" : "other";
    }

    private static JsonArray ToArray(IEnumerable<CsvRow> rows) => new(rows.Select(r => (JsonNode?)ToObject(r)).ToArray());

    private static JsonObject ToObject(CsvRow row)
    {
        var json = new JsonObject();
        foreach (var (key, value) in row)
        {
            json[key] = value;
        }

        return json;
    }

    private static IEnumerable<JsonObject> SortByName(IEnumerable<JsonObject> items) =>
        items.OrderBy(item => item["name"]!.GetValue<string>(), NameComparer.Instance);
}
