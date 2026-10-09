namespace Servitor.App.ArmyCollection.State;

/// <summary>My Collection's persistence (E4.3): saved armies survive a full app reload via <c>IRulesDataStore</c>.</summary>
public interface IArmyCollectionStore
{
    Task<IReadOnlyList<SavedArmy>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Upserts by id: replaces a saved army with the same id, or appends a new one.</summary>
    Task SaveAsync(SavedArmy army, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
