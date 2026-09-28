using Servitor.App.ArmyCollection.State;

namespace Servitor.App.Tests.ArmyCollection;

internal sealed class FakeArmyCollectionStore : IArmyCollectionStore
{
    private readonly List<SavedArmy> _armies = [];

    public IReadOnlyList<SavedArmy> Armies => _armies;

    public Task<IReadOnlyList<SavedArmy>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SavedArmy>>(_armies);

    public Task SaveAsync(SavedArmy army, CancellationToken cancellationToken = default)
    {
        _armies.Add(army);
        return Task.CompletedTask;
    }
}
