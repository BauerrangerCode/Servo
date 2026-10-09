using Servitor.App.BattleTracker.Units;

namespace Servitor.App.BattleTracker.State;

/// <summary>
/// The wounds, destroyed models and tokens of the player's own units (E6.6), per group of identical models. Persisted on
/// every change, like the battle progress. A group is named by <see cref="UnitKeys.GroupKey"/>. Models are destroyed and
/// brought back only through the wounds (see <see cref="GroupWounds"/>).
/// </summary>
public interface IUnitConditionState
{
    /// <summary>Raised after a change; the argument names the group that changed, or is null when any group may have.</summary>
    event Action<string?>? Changed;

    Task LoadAsync(CancellationToken cancellationToken = default);

    GroupCondition Of(string groupKey);

    /// <summary>Takes wounds (positive) or heals them (negative); never below no damage or above the group's total.</summary>
    Task AdjustDamageAsync(string groupKey, ModelGroup group, int delta);

    /// <summary>Heals the group completely, bringing every destroyed model back.</summary>
    Task RestoreGroupAsync(string groupKey);

    Task AddTokenAsync(string groupKey);

    Task RemoveTokenAsync(string groupKey);

    /// <summary>Forgets every condition, for a new game.</summary>
    Task ResetAsync();
}
