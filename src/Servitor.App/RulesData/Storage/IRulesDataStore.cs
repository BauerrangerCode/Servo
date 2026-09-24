namespace Servitor.App.RulesData.Storage;

/// <summary>
/// Hides IndexedDB/JS-interop details from all consumers. Rules data survives reloads and the app works fully
/// offline after the first load, since every access goes through this in-process interface.
/// </summary>
public interface IRulesDataStore
{
    Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default);

    Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default);
}
