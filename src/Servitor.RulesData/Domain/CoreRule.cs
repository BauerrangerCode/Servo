namespace Servitor.RulesData.Domain;

/// <summary>
/// A core rules entry. <paramref name="Number"/> is the rules-text source's section number where it has one.
/// <paramref name="Markdown"/> is BSData-style Markdown regardless of <paramref name="Source"/>.
/// </summary>
public sealed record CoreRule(
    string Id,
    string Name,
    string? Number,
    string Markdown,
    CoreRuleSource Source,
    CoreRuleKind Kind = CoreRuleKind.General);
