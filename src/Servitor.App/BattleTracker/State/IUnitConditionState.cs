using Servitor.App.BattleTracker.Units;

namespace Servitor.App.BattleTracker.State;

/// <summary>
/// The wounds, destroyed models and tokens of the player's own units (E6.6), per group of identical models. Persisted on
/// every change, like the battle progress. A group is named by <see cref="UnitKeys.GroupKey"/>.
/// </summary>
public interface IUnitConditionState
{
    event Action? Changed;

    Task LoadAsync(CancellationToken cancellationToken = default);

    GroupCondition Of(string groupKey);

    /// <summary>Takes wounds (positive) or heals them (negative); never below no damage or above the group's total.</summary>
    Task AdjustDamageAsync(string groupKey, ModelGroup group, int delta);

    Task KillModelAsync(string groupKey, ModelGroup group);

    /// <summary>Brings a destroyed model back, e.g. after a resurrection effect.</summary>
    Task ReviveModelAsync(string groupKey, ModelGroup group);

    Task DestroyGroupAsync(string groupKey, ModelGroup group);

    Task RestoreGroupAsync(string groupKey);

    Task AddTokenAsync(string groupKey);

    Task RemoveTokenAsync(string groupKey);

    Task SetCountsDownAsync(string groupKey, bool countsDown);

    /// <summary>Forgets every condition, for a new game.</summary>
    Task ResetAsync();
}
