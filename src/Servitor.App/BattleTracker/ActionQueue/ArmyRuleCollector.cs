using System.Text.RegularExpressions;
using Servitor.App.ArmyBuilder.State;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Html;
using Servitor.RulesData.Merging;

namespace Servitor.App.BattleTracker.ActionQueue;

/// <summary>
/// Gathers what the Army Rules section lists: the rules of the army's detachments and the named and wargear abilities
/// of its units (each datasheet once, however many copies are fielded). Leader and Support only say who may be
/// attached to whom, which is no play-time rule.
/// </summary>
public static partial class ArmyRuleCollector
{
    private static readonly string[] AttachmentAbilities = ["Leader", "Support"];

    [GeneratedRegex(@"\([^)]*\baura\b[^)]*\)", RegexOptions.IgnoreCase)]
    private static partial Regex AuraTag();

    public static IReadOnlyList<ArmyRuleEntry> Collect(MergedRulesData? data, ArmySetup setup, IReadOnlyList<ArmyUnit> units)
    {
        if (data is null)
        {
            return [];
        }

        return [.. DetachmentRules(data, setup), .. UnitAbilities(units)];
    }

    private static IEnumerable<ArmyRuleEntry> DetachmentRules(MergedRulesData data, ArmySetup setup)
    {
        var faction = data.Factions.FirstOrDefault(f => f.FactionId == setup.FactionId);
        if (faction is null)
        {
            yield break;
        }

        foreach (var detachment in setup.Detachments)
        {
            foreach (var rule in faction.Abilities.Where(a => string.Equals(a.Detachment, detachment.Name, StringComparison.OrdinalIgnoreCase)))
            {
                var markdown = HtmlToMarkdown.Convert(rule.DescriptionHtml);
                yield return new ArmyRuleEntry(
                    $"detachment|{detachment.Name}|{rule.Name}",
                    rule.Name,
                    detachment.Name,
                    ArmyRuleSource.Detachment,
                    markdown,
                    AbilityTiming.Of(markdown),
                    IsAura(rule.Name),
                    RuleOptionParser.Parse(rule.DescriptionHtml));
            }
        }
    }

    private static IEnumerable<ArmyRuleEntry> UnitAbilities(IReadOnlyList<ArmyUnit> units)
    {
        foreach (var datasheet in units.Select(u => u.Datasheet).DistinctBy(d => d.Name))
        {
            var abilities = datasheet.NamedAbilities.Concat(datasheet.WargearAbilities)
                .Where(a => !AttachmentAbilities.Contains(a.Name, StringComparer.OrdinalIgnoreCase));
            foreach (var ability in abilities)
            {
                yield return new ArmyRuleEntry(
                    $"unit|{datasheet.Name}|{ability.Name}",
                    ability.Name,
                    datasheet.Name,
                    ArmyRuleSource.Unit,
                    ability.Description,
                    AbilityTiming.Of(ability.Description),
                    IsAura(ability.Name),
                    []);
            }
        }
    }

    private static bool IsAura(string name) => AuraTag().IsMatch(name);
}
