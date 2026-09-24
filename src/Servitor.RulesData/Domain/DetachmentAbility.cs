namespace Servitor.RulesData.Domain;

/// <summary>
/// A detachment's Army Rule (the reference app's "Army Rules" / "Unit Abilities" content), distinct from
/// <see cref="Ability"/> (a datasheet's own named/wargear/core ability) since the two are mapped from
/// separate sources with different shapes.
/// </summary>
public sealed record DetachmentAbility(string Id, string Name, string Legend, string DescriptionHtml, string Detachment);
