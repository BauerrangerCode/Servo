using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Servitor.App.Shared.Viewport;

/// <summary>Wraps wwwroot/js/viewport.js so no component talks to JS interop directly.</summary>
public sealed class JsViewportInterop(IJSRuntime jsRuntime) : IViewportInterop, IAsyncDisposable
{
    private const string ModulePath = "./js/viewport.js";

    private readonly Lazy<Task<IJSObjectReference>> _module = new(() =>
        jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask());

    public async Task ScrollSelectedIntoViewAsync(ElementReference container)
    {
        var module = await _module.Value;
        await module.InvokeVoidAsync("scrollSelectedIntoView", container);
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
