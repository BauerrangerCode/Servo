using Microsoft.JSInterop;

namespace Servitor.App.Shared.Effects;

/// <summary>Wraps wwwroot/js/crtEffects.js so no component talks to JS interop directly.</summary>
public sealed class JsCrtEffectsService(IJSRuntime jsRuntime) : ICrtEffectsService, IAsyncDisposable
{
    private const string ModulePath = "./js/crtEffects.js";

    private readonly Lazy<Task<IJSObjectReference>> _module = new(() =>
        jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask());

    public async Task<CrtEffectsSettings> LoadAsync()
    {
        var module = await _module.Value;
        var stored = await module.InvokeAsync<CrtEffectsSettings?>("load");
        var settings = (stored ?? CrtEffectsSettings.Default).Normalized();
        await StartAsync(module, settings);
        return settings;
    }

    public async Task<CrtEffectsSettings> ApplyAsync(CrtEffectsSettings settings)
    {
        var module = await _module.Value;
        var normalized = settings.Normalized();
        await module.InvokeVoidAsync("save", normalized);
        await StartAsync(module, normalized);
        return normalized;
    }

    public async ValueTask DisposeAsync()
    {
        if (_module.IsValueCreated)
        {
            var module = await _module.Value;
            await module.InvokeVoidAsync("stop");
            await module.DisposeAsync();
        }
    }

    private static async Task StartAsync(IJSObjectReference module, CrtEffectsSettings settings)
    {
        if (settings.Enabled)
        {
            await module.InvokeVoidAsync("start", settings.MinSeconds, settings.MaxSeconds);
        }
        else
        {
            await module.InvokeVoidAsync("stop");
        }
    }
}
