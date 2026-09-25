using Servitor.App.ArmyBuilder.State;
using Servitor.RulesData.Merging;

namespace Servitor.App.ArmyBuilder.Rules;

/// <summary>The detachment rules read from the rules file, and what in the file could not be read.</summary>
public sealed record DetachmentRuleSet(IReadOnlyList<DetachmentRule> Rules, IReadOnlyList<string> Warnings)
{
    public static DetachmentRuleSet Empty { get; } = new([], []);

    /// <summary>The rules that apply to the army: those of its faction whose detachment it has selected.</summary>
    public IReadOnlyList<DetachmentRule> ActiveFor(ArmySetup setup)
    {
        if (setup.FactionId is not { } faction || Rules.Count == 0)
        {
            return [];
        }

        var selected = setup.Detachments.Select(d => NameNormalizer.Normalize(d.Name)).ToHashSet();
        return Rules
            .Where(r => r.Faction == faction && selected.Contains(NameNormalizer.Normalize(r.Detachment)))
            .ToList();
    }
}
