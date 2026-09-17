namespace Servitor.App.RulesData.Domain;

public sealed record WeaponProfile(
    string Name,
    WeaponType Type,
    string? Range,
    string Attacks,
    string Skill,
    string Strength,
    string ArmourPenetration,
    string Damage,
    IReadOnlyList<string> Keywords);
