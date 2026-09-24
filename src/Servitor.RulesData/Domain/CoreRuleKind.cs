namespace Servitor.RulesData.Domain;

/// <summary>
/// Distinguishes the kinds of core rules so a datasheet can later link to exactly one of them: weapon rules
/// (e.g. Blast) and unit rules (e.g. Deep Strike) are the rules a datasheet refers to by name, actions (e.g. Advance
/// Move) are what a unit does in a phase and cost no CP, general rules are the remaining rulebook prose.
/// </summary>
public enum CoreRuleKind
{
    General,
    WeaponRule,
    UnitRule,
    Action,
}
