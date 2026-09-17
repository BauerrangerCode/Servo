using System.Text.Json;
using Servitor.App.RulesData.Domain;
using Servitor.App.RulesData.Mapping.RulesText;
using Servitor.App.Shared.Html;

namespace Servitor.App.RulesData.Mapping;

public sealed record CoreStratagemsMappingResult(IReadOnlyList<Stratagem> Stratagems, IReadOnlyList<string> Warnings);

public sealed record FactionRulesTextMappingResult(
    IReadOnlyList<Stratagem> Stratagems,
    IReadOnlyList<DetachmentAbility> Abilities,
    IReadOnlyList<Enhancement> Enhancements,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Maps <c>/data/core/core-stratagems.json</c> and a faction's <c>/data/factions/&lt;faction&gt;/faction.json</c>
/// detachment stratagems/abilities/enhancements arrays into domain records — a separate mapper from
/// <see cref="BsDataCatalogueMapper"/> since this rules-text source has its own, unrelated file shape.
/// </summary>
public static class RulesTextMapper
{
    public static CoreStratagemsMappingResult MapCoreStratagems(string coreStratagemsJson)
    {
        List<RtStratagemEntry>? entries;
        try
        {
            entries = JsonSerializer.Deserialize<List<RtStratagemEntry>>(coreStratagemsJson);
        }
        catch (JsonException ex)
        {
            return new CoreStratagemsMappingResult([], [$"core-stratagems.json is not valid JSON: {ex.Message}"]);
        }

        if (entries is null)
        {
            return new CoreStratagemsMappingResult([], ["core-stratagems.json did not deserialize to a list."]);
        }

        var warnings = new List<string>();
        var stratagems = new List<Stratagem>();
        foreach (var entry in entries)
        {
            try
            {
                stratagems.Add(BuildStratagem(entry));
            }
            catch (Exception ex) when (ex is FormatException or NullReferenceException)
            {
                warnings.Add($"Skipped core stratagem '{entry.Name}': {ex.Message}");
            }
        }

        return new CoreStratagemsMappingResult(stratagems, warnings);
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
            return new FactionRulesTextMappingResult([], [], [], [$"faction.json is not valid JSON: {ex.Message}"]);
        }

        if (faction is null)
        {
            return new FactionRulesTextMappingResult([], [], [], ["faction.json has no recognizable content."]);
        }

        var warnings = new List<string>();
        var stratagems = new List<Stratagem>();
        var abilities = new List<DetachmentAbility>();
        var enhancements = new List<Enhancement>();

        foreach (var detachment in faction.Detachments)
        {
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

        return new FactionRulesTextMappingResult(stratagems, abilities, enhancements, warnings);
    }

    private static Stratagem BuildStratagem(RtStratagemEntry entry)
    {
        int? cpCost = int.TryParse(entry.CpCost, out var parsed) ? parsed : null;
        return new Stratagem(
            entry.Id,
            entry.Name,
            ClassifyStratagem(entry.Type),
            cpCost,
            entry.Legend,
            entry.Turn,
            entry.Phase,
            HtmlSanitizer.Sanitize(entry.Description),
            entry.Detachment);
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
