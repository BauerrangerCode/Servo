namespace Servitor.App.RulesData.Search;

public enum CharacteristicKind
{
    Toughness,
    Wounds,
}

/// <summary>An ad-hoc "at least N" filter on one numeric characteristic (E2.4).</summary>
public sealed record CharacteristicFilter(CharacteristicKind Kind, int MinimumValue);
