namespace Servitor.App.RulesData.Domain;

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
    CompositionConstraint? CompositionConstraint);
