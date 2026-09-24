using System.Text.Json;
using Microsoft.JSInterop;
using Servitor.RulesData.Merging;

namespace Servitor.App.RulesData.Storage;

/// <summary>
/// Thin JS-interop wrapper around the native IndexedDB API (see wwwroot/js/indexedDbInterop.js) — no ORM-style
/// NuGet dependency, per CODING_GUIDELINES.md. Each value is stored as one compact JSON document per key.
/// </summary>
public sealed class IndexedDbRulesDataStore(IJSRuntime jsRuntime) : IRulesDataStore, IAsyncDisposable
{
    private const string ModulePath = "./js/indexedDbInterop.js";

    private readonly Lazy<Task<IJSObjectReference>> _module = new(() =>
        jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask());

    public async Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        var module = await _module.Value;
        var json = JsonSerializer.Serialize(value, MergedDataJson.Options);
        await module.InvokeVoidAsync("putDocument", cancellationToken, key, json);
    }

    public async Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var module = await _module.Value;
        var json = await module.InvokeAsync<string?>("getDocument", cancellationToken, key);
        return json is null ? default : JsonSerializer.Deserialize<T>(json, MergedDataJson.Options);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module.IsValueCreated)
        {
            var module = await _module.Value;
            await module.DisposeAsync();
        }
    }
}
