using Servitor.App.ArmyBuilder.Rules;
using Servitor.App.ArmyBuilder.State;

namespace Servitor.App.ArmyBuilder.Validation;

/// <summary>What the rules look at: the setup, the units as listed, and the same units priced with the army's totals.</summary>
public sealed record ArmyContext(
    ArmySetup Setup,
    IReadOnlyList<ArmyUnit> Units,
    IReadOnlyList<PricedUnit> Priced,
    ArmyTotals Totals,
    IReadOnlyList<DetachmentRule> ActiveRules)
{
    /// <param name="rules">The detachment rules in force; the ones that apply to the army's detachments are picked out here.</param>
    public static ArmyContext Create(ArmySetup setup, IReadOnlyList<ArmyUnit> units, DetachmentRuleSet? rules = null)
    {
        var priced = ArmyPricing.Price(units);
        return new ArmyContext(setup, units, priced, ArmyTotals.Of(priced), (rules ?? DetachmentRuleSet.Empty).ActiveFor(setup));
    }
}

/// <summary>One composition rule of an army list. Each rule type is its own class so it is tested on its own.</summary>
public interface IArmyRule
{
    IEnumerable<ArmyIssue> Check(ArmyContext context);
}
