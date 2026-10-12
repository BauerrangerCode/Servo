using Microsoft.JSInterop;

namespace Servitor.App.Shared.WakeLock;

/// <summary>Wraps wwwroot/js/wakeLock.js so no component talks to JS interop directly.</summary>
public sealed class JsWakeLockService(IJSRuntime jsRuntime) : IWakeLockService, IAsyncDisposable
{
    private const string ModulePath = "./js/wakeLock.js";

    private readonly Lazy<Task<IJSObjectReference>> _module = new(() =>
        jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask());

    public async Task<bool> AcquireAsync()
    {
        var module = await _module.Value;
        return await module.InvokeAsync<bool>("acquire");
    }

    public async Task ReleaseAsync()
    {
        var module = await _module.Value;
        await module.InvokeVoidAsync("release");
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
