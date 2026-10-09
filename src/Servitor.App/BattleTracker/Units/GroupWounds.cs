namespace Servitor.App.BattleTracker.Units;

/// <summary>
/// The wound arithmetic of a group of identical models. Damage is taken by one model at a time, so a group that has
/// taken <c>d</c> wounds with <c>w</c> wounds per model has <c>d / w</c> models destroyed and the next one hurt by <c>d % w</c>.
/// </summary>
public static class GroupWounds
{
    public static int MaxDamage(ModelGroup group) => group.Models * group.WoundsPerModel;

    public static int Destroyed(ModelGroup group, GroupCondition condition) =>
        Math.Min(group.Models, condition.Damage / group.WoundsPerModel);

    public static int Alive(ModelGroup group, GroupCondition condition) => group.Models - Destroyed(group, condition);

    public static bool IsDestroyed(ModelGroup group, GroupCondition condition) => Alive(group, condition) == 0;

    public static int WoundsRemaining(ModelGroup group, GroupCondition condition) => MaxDamage(group) - condition.Damage;

    /// <summary>Wounds taken by the one model currently being hurt; 0 when none is.</summary>
    public static int WoundedModelDamage(ModelGroup group, GroupCondition condition) =>
        IsDestroyed(group, condition) ? 0 : condition.Damage % group.WoundsPerModel;

    public static GroupCondition Adjust(ModelGroup group, GroupCondition condition, int delta) =>
        condition with { Damage = Math.Clamp(condition.Damage + delta, 0, MaxDamage(group)) };

    /// <summary>Destroys the next model of the group, whatever damage it already took.</summary>
    public static GroupCondition KillModel(ModelGroup group, GroupCondition condition) =>
        IsDestroyed(group, condition)
            ? condition
            : condition with { Damage = Math.Min(MaxDamage(group), (Destroyed(group, condition) + 1) * group.WoundsPerModel) };

    /// <summary>Brings one destroyed model back with the wounds of the group left as they were.</summary>
    public static GroupCondition ReviveModel(ModelGroup group, GroupCondition condition) =>
        Destroyed(group, condition) == 0
            ? condition
            : condition with { Damage = condition.Damage - group.WoundsPerModel };

    public static GroupCondition Destroy(ModelGroup group, GroupCondition condition) =>
        condition with { Damage = MaxDamage(group) };

    public static GroupCondition Restore(GroupCondition condition) => condition with { Damage = 0 };
}
