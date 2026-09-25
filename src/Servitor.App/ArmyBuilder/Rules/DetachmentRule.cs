namespace Servitor.App.ArmyBuilder.Rules;

/// <summary>
/// A composition rule a detachment adds to the army ("your army must include three or more WAR DOG units"). The rules
/// are kept as data, not code — see <see cref="DetachmentRuleParser"/> and <c>wwwroot/rules/detachment-rules.json</c> —
/// so a change of the rules is a change of that file.
/// </summary>
/// <param name="Faction">The army faction (rules-text slug) the detachment belongs to.</param>
/// <param name="Detachment">The detachment's name; the rule applies while the army includes it.</param>
/// <param name="Label">What the rule is about, as the messages name it ("WAR DOG units").</param>
/// <param name="Units">The units the rule is about.</param>
public abstract record DetachmentRule(string Faction, string Detachment, string Label, UnitSelector Units);

/// <summary>The army has to include at least <paramref name="Min"/> units the selector matches.</summary>
public sealed record MustIncludeRule(string Faction, string Detachment, string Label, UnitSelector Units, int Min)
    : DetachmentRule(Faction, Detachment, Label, Units);

/// <summary>The army may not include any unit the selector matches.</summary>
public sealed record CannotIncludeRule(string Faction, string Detachment, string Label, UnitSelector Units)
    : DetachmentRule(Faction, Detachment, Label, Units);

/// <summary>The units the selector matches may together cost at most the points given for the battle size.</summary>
/// <param name="CapsByBattleSize">The cap in points by battle size name ("Incursion", "Strike Force", "Onslaught").</param>
public sealed record PointsCapRule(
    string Faction,
    string Detachment,
    string Label,
    UnitSelector Units,
    IReadOnlyDictionary<string, int> CapsByBattleSize)
    : DetachmentRule(Faction, Detachment, Label, Units);

/// <summary>The army may take up to <paramref name="Max"/> copies of each unit the selector matches, whatever the datasheet's own limit says.</summary>
public sealed record UnitLimitOverrideRule(string Faction, string Detachment, string Label, UnitSelector Units, int Max)
    : DetachmentRule(Faction, Detachment, Label, Units);
