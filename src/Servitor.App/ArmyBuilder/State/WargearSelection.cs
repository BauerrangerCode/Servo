namespace Servitor.App.ArmyBuilder.State;

/// <summary>
/// What a unit's player chose from its wargear options: the picked choice of each weapon slot (by index) and how
/// many models take each pool item. Anything not listed is at its default (the slot's default choice, no models).
/// </summary>
public sealed record WargearSelection(
    IReadOnlyDictionary<string, int> SlotChoices,
    IReadOnlyDictionary<string, int> PoolCounts)
{
    public static WargearSelection Default { get; } = new(new Dictionary<string, int>(), new Dictionary<string, int>());
}
