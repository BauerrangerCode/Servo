namespace Servitor.RulesData.Domain;

/// <summary>
/// <paramref name="StratagemIds"/> are the ids of the stratagems the rules-text source lists for this unit; they are
/// empty straight after BSData mapping and only filled in by the data merge. The same holds for <see cref="Sizes"/>
/// (the model counts the unit can be fielded with, cheapest first) and <see cref="FactionSlugs"/> (the armies, as
/// rules-text faction slugs, that can take the unit — one unit can belong to several).
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
    IReadOnlyList<string> StratagemIds)
{
    public IReadOnlyList<UnitSize> Sizes { get; init; } = [];

    public IReadOnlyList<string> FactionSlugs { get; init; } = [];
}
