using System.Text.Json;
using Microsoft.JSInterop;

namespace Servitor.App.RulesData.Storage;

/// <summary>
/// Thin JS-interop wrapper around the native IndexedDB API (see wwwroot/js/indexedDbInterop.js) — no ORM-style
/// NuGet dependency, per CODING_GUIDELINES.md. Faction data is stored as one compact JSON document per key.
/// </summary>
public sealed class IndexedDbRulesDataStore(IJSRuntime jsRuntime) : IRulesDataStore, IAsyncDisposable
{
    private const string ModulePath = "./js/indexedDbInterop.js";

    private readonly Lazy<Task<IJSObjectReference>> _module = new(() =>
        jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask());

    public async Task SaveFactionAsync(FactionRulesData data, CancellationToken cancellationToken = default)
    {
        var module = await _module.Value;
        var key = BuildKey(data.FactionId, data.DataVersion);
        var json = JsonSerializer.Serialize(data);
        await module.InvokeVoidAsync("putFaction", cancellationToken, key, json);
    }

    public async Task<FactionRulesData?> LoadFactionAsync(
        string factionId, string dataVersion, CancellationToken cancellationToken = default)
    {
        var module = await _module.Value;
        var key = BuildKey(factionId, dataVersion);
        var json = await module.InvokeAsync<string?>("getFaction", cancellationToken, key);
        return json is null ? null : JsonSerializer.Deserialize<FactionRulesData>(json);
    }

    private static string BuildKey(string factionId, string dataVersion) => $"{factionId}::{dataVersion}";

    public async ValueTask DisposeAsync()
    {
        if (_module.IsValueCreated)
        {
            var module = await _module.Value;
            await module.DisposeAsync();
        }
    }
}
