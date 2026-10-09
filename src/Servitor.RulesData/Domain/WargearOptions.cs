namespace Servitor.RulesData.Domain;

public enum ModelCountOperator
{
    AtLeast,
    GreaterThan,
    AtMost,
    LessThan,
    EqualTo,
}

/// <summary>A condition on how many models the unit has ("at least 20 models").</summary>
public sealed record ModelCountCondition(ModelCountOperator Operator, int Value);

/// <summary>
/// A change to a limit that applies while all <paramref name="Conditions"/> hold: it either sets the limit to
/// <paramref name="Value"/> or, when <paramref name="Sets"/> is false, adds <paramref name="Value"/> to it.
/// </summary>
public sealed record LimitAdjustment(bool Sets, int Value, IReadOnlyList<ModelCountCondition> Conditions);

/// <summary>A maximum that can grow with the size of the unit: the <paramref name="Base"/> value, changed by the adjustments in order.</summary>
public sealed record WargearLimit(int Base, IReadOnlyList<LimitAdjustment> Adjustments);

/// <summary>One weapon slot of a unique model, taking exactly one of the <paramref name="Choices"/>.</summary>
public sealed record WargearSlot(string Name, IReadOnlyList<string> Choices, int DefaultIndex);

/// <summary>A unique model of the unit (a sergeant, a character) and the weapon slots it can configure.</summary>
public sealed record WargearModel(string ModelName, IReadOnlyList<WargearSlot> Slots);

/// <summary>
/// A weapon a number of the unit's identical models can take, optionally instead of <paramref name="Replaces"/>. The
/// number is capped by every one of the <paramref name="Limits"/> (the smallest wins).
/// </summary>
public sealed record WargearPool(string Item, string? Replaces, IReadOnlyList<WargearLimit> Limits);

/// <summary>The wargear a unit can configure, in the two shapes the army builder offers.</summary>
public sealed record WargearOptions(IReadOnlyList<WargearModel> Models, IReadOnlyList<WargearPool> Pools)
{
    public bool IsEmpty => Models.Count == 0 && Pools.Count == 0;
}

/// <summary>What one wargear item adds to the unit's points each time it is taken.</summary>
public sealed record WargearCost(string Item, int Points);
