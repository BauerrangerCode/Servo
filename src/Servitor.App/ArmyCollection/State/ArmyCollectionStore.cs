using Servitor.App.RulesData.Storage;

namespace Servitor.App.ArmyCollection.State;

public sealed class ArmyCollectionStore(IRulesDataStore store) : IArmyCollectionStore
{
    private const string Key = "army-collection/armies";

    public async Task<IReadOnlyList<SavedArmy>> ListAsync(CancellationToken cancellationToken = default) =>
        await store.LoadAsync<List<SavedArmy>>(Key, cancellationToken) ?? [];

    public async Task SaveAsync(SavedArmy army, CancellationToken cancellationToken = default)
    {
        var armies = (await ListAsync(cancellationToken)).ToList();
        armies.Add(army);
        await store.SaveAsync(Key, armies, cancellationToken);
    }
}
