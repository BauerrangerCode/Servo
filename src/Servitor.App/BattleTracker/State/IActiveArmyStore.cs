using Servitor.App.ArmyBuilder.State;

namespace Servitor.App.BattleTracker.State;

/// <summary>
/// Keeps the army Play works with across a reload, since the army builder itself only lives in memory. The wounds saved
/// for its units (see <see cref="IUnitConditionState"/>) are only of use if the same army comes back.
/// </summary>
public interface IActiveArmyStore
{
    Task SaveAsync(IArmyBuilderState army, CancellationToken cancellationToken = default);

    /// <summary>Loads the saved army into <paramref name="army"/>; false, changing nothing, when none is saved or it no longer resolves.</summary>
    Task<bool> RestoreAsync(IArmyBuilderState army, CancellationToken cancellationToken = default);
}
