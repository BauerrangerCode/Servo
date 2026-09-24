namespace Servitor.RulesData.Domain;

/// <summary>
/// <paramref name="StratagemIds"/> are the ids of the stratagems the rules-text source lists for this unit; they are
/// empty straight after BSData mapping and only filled in by the data merge.
/// </summary>
public sealed record Datasheet(
    string Name,
    string Faction,
    int Points,
    StatLine StatLine,
    IReadOnlyList<WeaponProfile> RangedWeapons,
    IReadOnlyList<WeaponProfile> MeleeWeapons,
    IReadOnlyList<Ability> NamedAbilities,
    IReadOnlyList<Ability> WargearAbilities,
    IReadOnlyList<Ability> CoreAbilities,
    IReadOnlyList<string> Keywords,
    CompositionConstraint? CompositionConstraint,
    IReadOnlyList<string> StratagemIds);
