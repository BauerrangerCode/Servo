using Servitor.App.ArmyBuilder.State;
using Servitor.App.ArmyCollection.Resolution;
using Servitor.App.ArmyCollection.State;
using Servitor.App.RulesData.Loading;
using Servitor.App.RulesData.Storage;

namespace Servitor.App.BattleTracker.State;

public sealed class ActiveArmyStore(IRulesDataStore store, IMergedRulesDataProvider rulesData, IDatasheetCatalog catalog) : IActiveArmyStore
{
    private const string Key = "battle/active-army";
    private const string ArmyName = "Active army";

    public async Task SaveAsync(IArmyBuilderState army, CancellationToken cancellationToken = default)
    {
        if (army.Units.Count > 0 && army.Setup is { FactionId: not null, BattleSize: not null })
        {
            await store.SaveAsync(Key, ArmySnapshot.Capture(ArmyName, army), cancellationToken);
        }
    }

    public async Task<bool> RestoreAsync(IArmyBuilderState army, CancellationToken cancellationToken = default)
    {
        if (await store.LoadAsync<SavedArmy>(Key, cancellationToken) is not { } saved)
        {
            return false;
        }

        var result = await SavedArmyResolver.ResolveAsync(saved, army, rulesData, catalog, cancellationToken);
        if (!result.IsSuccess)
        {
            army.Reset();
        }

        return result.IsSuccess;
    }
}
