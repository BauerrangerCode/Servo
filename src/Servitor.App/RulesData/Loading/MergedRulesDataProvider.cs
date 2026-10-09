using System.Text.Json;
using Servitor.App.RulesData.Storage;
using Servitor.RulesData.Merging;

namespace Servitor.App.RulesData.Loading;

public sealed class MergedRulesDataProvider(HttpClient httpClient, IRulesDataStore store) : IMergedRulesDataProvider
{
    private const string StoredManifestKey = "merged-rules-data/manifest";
    private const string StoredDataKey = "merged-rules-data/data";

    private MergedRulesData? _cache;

    public event Action? DataChanged;

    public async Task<MergedRulesData?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cache is not null)
        {
            return _cache;
        }

        var storedManifest = await TryLoadStoredAsync<MergedDataManifest>(StoredManifestKey, cancellationToken);
        var shippedManifest = await TryFetchShippedAsync<MergedDataManifest>(RulesDataUrls.MergedManifest, cancellationToken);

        var storedIsCurrent = storedManifest is not null
            && (shippedManifest is null || storedManifest.GeneratedAtUtc >= shippedManifest.GeneratedAtUtc);
        if (storedIsCurrent)
        {
            _cache = await TryLoadStoredAsync<MergedRulesData>(StoredDataKey, cancellationToken);
        }

        if (_cache is null && shippedManifest is not null)
        {
            _cache = await TryFetchShippedAsync<MergedRulesData>(RulesDataUrls.MergedData, cancellationToken);
            if (_cache is not null)
            {
                await PersistAsync(_cache, cancellationToken);
            }
        }

        return _cache;
    }

    public async Task StoreAsync(MergedRulesData data, CancellationToken cancellationToken = default)
    {
        await PersistAsync(data, cancellationToken);
        _cache = data;
        DataChanged?.Invoke();
    }

    // The data is saved before the manifest that vouches for it, so an interrupted write leaves an older (or no)
    // manifest and the next start simply loads the shipped files again.
    private async Task PersistAsync(MergedRulesData data, CancellationToken cancellationToken)
    {
        await store.SaveAsync(StoredDataKey, data, cancellationToken);
        await store.SaveAsync(StoredManifestKey, new MergedDataManifest(data.DataVersion, data.GeneratedAtUtc), cancellationToken);
    }

    // A stored document written by an older schema no longer deserializes; treating it as absent lets the shipped
    // files (or a fresh merge) replace it instead of failing every load.
    private async Task<T?> TryLoadStoredAsync<T>(string key, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            return await store.LoadAsync<T>(key, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // No shipped merge output (never built, or offline without a cached copy) is an ordinary state, not an error.
    private async Task<T?> TryFetchShippedAsync<T>(string url, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var response = await httpClient.SendAsync(RulesDataUrls.Get(url), cancellationToken);
            if (!response.IsSuccessStatusCode || !RulesDataUrls.IsJson(response))
            {
                return null;
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<T>(body, MergedDataJson.Options, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return null;
        }
    }
}
