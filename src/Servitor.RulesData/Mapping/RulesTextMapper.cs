using System.Globalization;
using System.Text.Json;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Html;
using Servitor.RulesData.Mapping.RulesText;

namespace Servitor.RulesData.Mapping;

public sealed record CoreStratagemsMappingResult(
    IReadOnlyList<Stratagem> Stratagems,
    IReadOnlyList<CoreRule> Actions,
    IReadOnlyList<string> Warnings);

public sealed record FactionRulesTextMappingResult(
    IReadOnlyList<Stratagem> Stratagems,
    IReadOnlyList<DetachmentAbility> Abilities,
    IReadOnlyList<Enhancement> Enhancements,
    IReadOnlyList<Detachment> Detachments,
    FactionIdentity Identity,
    IReadOnlyList<DatasheetReference> Datasheets,
    IReadOnlyList<string> Warnings);

public sealed record FactionIdentity(string Slug, string Name, string DataVersion);

/// <summary>A datasheet as listed in <c>faction.json</c>; its detail file is <c>datasheets/&lt;Id&gt;.json</c> beside it.</summary>
public sealed record DatasheetReference(string Id, string Name, bool IsLegends);

/// <summary>The parts of a per-datasheet detail file the data merge needs: its stratagems and the sizes it can be fielded in.</summary>
public sealed record DatasheetDetails(
    string Id,
    IReadOnlyList<string> StratagemIds,
    IReadOnlyList<CostBracket> CostBrackets,
    IReadOnlyList<WargearCost> WargearCosts)
{
    public IReadOnlyList<UnitSize> Sizes => CostBrackets.FirstOrDefault()?.Sizes ?? [];
}

/// <summary>
/// Maps the rules-text source (<c>core/*.json</c> and each faction's <c>faction.json</c> with its per-datasheet
/// files) into domain records — a separate mapper from <see cref="BsDataCatalogueMapper"/> since this source has
/// its own, unrelated file shape.
/// </summary>
public static class RulesTextMapper
{
    private const char NonBreakingHyphen = '‑';
    private const string BoardingActionsLabel = "Boarding Actions";
    private const string ChallengerLabel = "Challenger";

    public static CoreStratagemsMappingResult MapCoreStratagems(string coreStratagemsJson)
    {
        List<RtStratagemEntry>? entries;
        try
        {
            entries = JsonSerializer.Deserialize<List<RtStratagemEntry>>(coreStratagemsJson);
        }
        catch (JsonException ex)
        {
            return new CoreStratagemsMappingResult([], [], [$"core-stratagems.json is not valid JSON: {ex.Message}"]);
        }

        if (entries is null)
        {
            return new CoreStratagemsMappingResult([], [], ["core-stratagems.json did not deserialize to a list."]);
        }

        var warnings = new List<string>();
        var stratagems = new List<Stratagem>();
        var actions = new List<CoreRule>();
        foreach (var entry in entries)
        {
            try
            {
                if (HasCpCost(entry))
                {
                    stratagems.Add(BuildStratagem(entry));
                }
                else
                {
                    actions.Add(BuildAction(entry));
                }
            }
            catch (Exception ex) when (ex is FormatException or NullReferenceException)
            {
                warnings.Add($"Skipped core stratagem '{entry.Name}': {ex.Message}");
            }
        }

        return new CoreStratagemsMappingResult(stratagems, actions, warnings);
    }

    public static FactionRulesTextMappingResult MapFaction(string factionJson)
    {
        RtFactionDocument? faction;
        try
        {
            faction = JsonSerializer.Deserialize<RtFactionDocument>(factionJson);
        }
        catch (JsonException ex)
        {
            return EmptyFactionResult($"faction.json is not valid JSON: {ex.Message}");
        }

        if (faction is null)
        {
            return EmptyFactionResult("faction.json has no recognizable content.");
        }

        var warnings = new List<string>();
        var stratagems = new List<Stratagem>();
        var abilities = new List<DetachmentAbility>();
        var enhancements = new List<Enhancement>();
        var detachments = new List<Detachment>();

        foreach (var detachment in faction.Detachments)
        {
            detachments.Add(BuildDetachment(detachment));

            foreach (var entry in detachment.Stratagems)
            {
                try
                {
                    stratagems.Add(BuildStratagem(entry));
                }
                catch (Exception ex) when (ex is FormatException or NullReferenceException)
                {
                    warnings.Add($"Skipped stratagem '{entry.Name}' in detachment '{detachment.Name}': {ex.Message}");
                }
            }

            foreach (var entry in detachment.Abilities)
            {
                try
                {
                    abilities.Add(new DetachmentAbility(
                        entry.Id, entry.Name, entry.Legend, HtmlSanitizer.Sanitize(entry.Description), entry.Detachment));
                }
                catch (NullReferenceException ex)
                {
                    warnings.Add($"Skipped ability '{entry.Name}' in detachment '{detachment.Name}': {ex.Message}");
                }
            }

            foreach (var entry in detachment.Enhancements)
            {
                try
                {
                    enhancements.Add(BuildEnhancement(entry));
                }
                catch (Exception ex) when (ex is FormatException or NullReferenceException)
                {
                    warnings.Add($"Skipped enhancement '{entry.Name}' in detachment '{detachment.Name}': {ex.Message}");
                }
            }
        }

        var datasheets = faction.Datasheets
            .Select(d => new DatasheetReference(d.Id, d.Name, d.IsLegends))
            .ToList();

        return new FactionRulesTextMappingResult(
            stratagems,
            abilities,
            enhancements,
            detachments,
            new FactionIdentity(faction.Slug, faction.Name, faction.DataVersion),
            datasheets,
            warnings);
    }

    public static DatasheetDetails? MapDatasheetDetails(string datasheetJson)
    {
        try
        {
            var datasheet = JsonSerializer.Deserialize<RtDatasheetDocument>(datasheetJson);
            return datasheet is null
                ? null
                : new DatasheetDetails(datasheet.Id, datasheet.StratagemIds, UnitSizeParser.ParseBrackets(datasheet.ModelCosts),
                    UnitSizeParser.ParseWargearCosts(datasheet.ModelCosts));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static FactionRulesTextMappingResult EmptyFactionResult(string warning) =>
        new([], [], [], [], new FactionIdentity("", "", ""), [], [warning]);

    public static CoreRulesMappingResult MapCoreRules(string coreRulesJson)
    {
        List<RtCoreRuleEntry>? entries;
        try
        {
            entries = JsonSerializer.Deserialize<List<RtCoreRuleEntry>>(coreRulesJson);
        }
        catch (JsonException ex)
        {
            return new CoreRulesMappingResult([], [$"Core rules file is not valid JSON: {ex.Message}"]);
        }

        if (entries is null)
        {
            return new CoreRulesMappingResult([], ["Core rules file did not deserialize to a list."]);
        }

        var rules = entries
            .Select(e => new CoreRule(e.Id, e.Name, e.Number, HtmlToMarkdown.Convert(e.Description), CoreRuleSource.RulesText))
            .ToList();
        return new CoreRulesMappingResult(rules, []);
    }

    // Only stratagems cost CP. The file also lists actions ("Advance Move", "Normal Shooting", ...) without a cost.
    private static bool HasCpCost(RtStratagemEntry entry) => int.TryParse(entry.CpCost, out _);

    private static CoreRule BuildAction(RtStratagemEntry entry) => new(
        entry.Id,
        ToActionName(entry.Name),
        null,
        HtmlToMarkdown.Convert(entry.Description),
        CoreRuleSource.RulesText,
        CoreRuleKind.Action);

    // The source writes action names in capitals and sometimes with a non-breaking hyphen.
    private static string ToActionName(string sourceName) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(sourceName.Replace(NonBreakingHyphen, '-').ToLowerInvariant());

    private static Stratagem BuildStratagem(RtStratagemEntry entry)
    {
        int? cpCost = int.TryParse(entry.CpCost, out var parsed) ? parsed : null;
        var text = StratagemTextParser.Parse(HtmlToMarkdown.Convert(entry.Description));
        return new Stratagem(
            entry.Id,
            entry.Name,
            ClassifyStratagem(entry.Type),
            cpCost,
            entry.Legend,
            entry.Turn,
            entry.Phase,
            text.When,
            text.Target,
            text.Effect,
            text.Restrictions,
            text.AdditionalText,
            entry.Detachment,
            ClassifyGameMode(entry.Type));
    }

    private static Enhancement BuildEnhancement(RtEnhancementEntry entry)
    {
        var cost = int.TryParse(entry.Cost, out var parsed) ? parsed : 0;
        return new Enhancement(
            entry.Id,
            entry.Name,
            entry.Legend,
            HtmlSanitizer.Sanitize(entry.Description),
            cost,
            entry.Detachment,
            entry.Upgrade,
            entry.SupportLeader is null ? null : HtmlSanitizer.Sanitize(entry.SupportLeader));
    }

    private static Detachment BuildDetachment(RtDetachment entry) => new(
        entry.Id,
        entry.Name,
        HtmlToMarkdown.Convert(entry.Legend),
        int.TryParse(entry.Dp, out var points) ? points : 0,
        ClassifyArchetype(entry.ForceDisposition),
        entry.Enhancements.Count,
        ClassifyGameMode(entry.Type))
    {
        RestrictedToChapter = ChapterRestrictionParser.Find(entry.Abilities.Select(a => a.Description).Append(entry.Legend)),
        RuleText = BuildDetachmentRuleText(entry.Abilities),
    };

    // The detachment's own "legend" field (see BuildDetachment above) is only a short flavor quote; the actual rule
    // — what the detachment does mechanically — is one or more named abilities the source lists separately.
    private static string? BuildDetachmentRuleText(IReadOnlyList<RtAbilityEntry> abilities) =>
        abilities.Count == 0
            ? null
            : string.Join("\n\n", abilities.Select(a => $"**{a.Name}**\n\n{HtmlToMarkdown.Convert(a.Description)}"));

    private static MissionArchetype? ClassifyArchetype(string label) => label switch
    {
        "Take and Hold" => MissionArchetype.TakeAndHold,
        "Disruption" => MissionArchetype.Disruption,
        "Priority Assets" => MissionArchetype.PriorityAssets,
        "Purge the Foe" => MissionArchetype.PurgeTheFoe,
        "Reconnaissance" => MissionArchetype.Reconnaissance,
        _ => null,
    };

    private static GameMode ClassifyGameMode(string typeLabel)
    {
        if (typeLabel.StartsWith(BoardingActionsLabel, StringComparison.OrdinalIgnoreCase))
        {
            return GameMode.BoardingActions;
        }

        return typeLabel.StartsWith(ChallengerLabel, StringComparison.OrdinalIgnoreCase)
            ? GameMode.Challenger
            : GameMode.Standard;
    }

    private static StratagemCategory ClassifyStratagem(string typeLabel)
    {
        if (typeLabel.Contains("Battle Tactic", StringComparison.OrdinalIgnoreCase))
        {
            return StratagemCategory.BattleTactic;
        }

        if (typeLabel.Contains("Strategic Ploy", StringComparison.OrdinalIgnoreCase))
        {
            return StratagemCategory.StrategicPloy;
        }

        if (typeLabel.Contains("Epic Deed", StringComparison.OrdinalIgnoreCase))
        {
            return StratagemCategory.EpicDeed;
        }

        if (typeLabel.Contains("Wargear", StringComparison.OrdinalIgnoreCase))
        {
            return StratagemCategory.Wargear;
        }

        return StratagemCategory.Other;
    }
}
