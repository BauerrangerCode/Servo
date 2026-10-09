using System.Text.Json;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Mapping.BsData;

namespace Servitor.RulesData.Mapping;

public sealed record CoreRulesMappingResult(IReadOnlyList<CoreRule> Rules, IReadOnlyList<string> Warnings);

/// <summary>
/// Maps the BSData game-system file's shared rules (weapon keywords, core abilities such as Deep Strike) into
/// <see cref="CoreRule"/> records. The description text is already Markdown; only BSData's <c>^^</c> keyword markers are removed.
/// </summary>
public static class BsDataGameSystemMapper
{
    private const string WeaponKeywordOpening = "**[";

    public static CoreRulesMappingResult Map(string gameSystemJson)
    {
        BsGameSystemDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<BsGameSystemDocument>(gameSystemJson);
        }
        catch (JsonException ex)
        {
            return new CoreRulesMappingResult([], [$"Game system is not valid JSON: {ex.Message}"]);
        }

        if (document?.GameSystem is not { } gameSystem)
        {
            return new CoreRulesMappingResult([], ["Game system document has no 'gameSystem' root."]);
        }

        var rules = gameSystem.SharedRules
            .Select(rule => new CoreRule(rule.Id, rule.Name, null, BsDataText.CleanMarkdown(rule.Description), CoreRuleSource.BsData, Classify(rule)))
            .ToList();

        return new CoreRulesMappingResult(rules, []);
    }

    // BSData has no field for it; a weapon rule is the one whose text names it as a bracketed weapon keyword,
    // e.g. "**[BLAST]**", while unit rules such as Deep Strike are never written that way.
    private static CoreRuleKind Classify(BsRule rule) =>
        rule.Description.Contains(WeaponKeywordOpening + rule.Name, StringComparison.OrdinalIgnoreCase)
            ? CoreRuleKind.WeaponRule
            : CoreRuleKind.UnitRule;
}
