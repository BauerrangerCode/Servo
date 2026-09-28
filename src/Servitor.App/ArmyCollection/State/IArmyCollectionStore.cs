namespace Servitor.App.ArmyCollection.State;

/// <summary>My Collection's persistence (E4.3): saved armies survive a full app reload via <c>IRulesDataStore</c>.</summary>
public interface IArmyCollectionStore
{
    Task<IReadOnlyList<SavedArmy>> ListAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(SavedArmy army, CancellationToken cancellationToken = default);
}
