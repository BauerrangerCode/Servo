namespace Servitor.RulesData.Domain;

public sealed record StatLine(
    string Movement,
    string Toughness,
    string Save,
    string? InvulnerableSave,
    string Wounds,
    string Leadership,
    string ObjectiveControl);
