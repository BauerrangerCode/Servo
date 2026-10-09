namespace Servitor.App.BattleTracker.Units;

/// <summary>
/// The wound arithmetic of a group of identical models. Damage is taken by one model at a time, so a group that has
/// taken <c>d</c> wounds with <c>w</c> wounds per model has <c>d / w</c> models destroyed and the next one hurt by
/// <c>d % w</c>. Healing runs the same way backwards: the wound that heals past the surviving model's maximum brings a
/// destroyed model back (with that one wound). The total can never go below none or above the models times their wounds.
/// </summary>
public static class GroupWounds
{
    public static int MaxDamage(ModelGroup group) => group.Models * group.WoundsPerModel;

    public static int Destroyed(ModelGroup group, GroupCondition condition) =>
        Math.Min(group.Models, condition.Damage / group.WoundsPerModel);

    public static int Alive(ModelGroup group, GroupCondition condition) => group.Models - Destroyed(group, condition);

    public static bool IsDestroyed(ModelGroup group, GroupCondition condition) => Alive(group, condition) == 0;

    public static int WoundsRemaining(ModelGroup group, GroupCondition condition) => MaxDamage(group) - condition.Damage;

    /// <summary>The wounds the model currently being hurt has left; the full amount when none is hurt, 0 when the group is destroyed.</summary>
    public static int WoundedModelRemaining(ModelGroup group, GroupCondition condition) =>
        IsDestroyed(group, condition) ? 0 : group.WoundsPerModel - condition.Damage % group.WoundsPerModel;

    /// <summary>Takes wounds (positive) or heals them (negative).</summary>
    public static GroupCondition Adjust(ModelGroup group, GroupCondition condition, int delta) =>
        condition with { Damage = Math.Clamp(condition.Damage + delta, 0, MaxDamage(group)) };

    public static GroupCondition Restore(GroupCondition condition) => condition with { Damage = 0 };
}
