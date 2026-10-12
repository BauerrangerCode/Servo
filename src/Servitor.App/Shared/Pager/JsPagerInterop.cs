using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Servitor.App.Shared.Pager;

/// <summary>Wraps wwwroot/js/pager.js so no component talks to JS interop directly.</summary>
public sealed class JsPagerInterop(IJSRuntime jsRuntime) : IPagerInterop, IAsyncDisposable
{
    private const string ModulePath = "./js/pager.js";

    private readonly Lazy<Task<IJSObjectReference>> _module = new(() =>
        jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask());

    public async Task<IAsyncDisposable> AttachAsync(ElementReference track, Func<int, Task> onPageChanged)
    {
        var module = await _module.Value;
        var callback = DotNetObjectReference.Create(new PageChangedCallback(onPageChanged));
        await module.InvokeVoidAsync("attach", track, callback);
        return new Attachment(module, track, callback);
    }

    public async Task ScrollToAsync(ElementReference track, int index)
    {
        var module = await _module.Value;
        await module.InvokeVoidAsync("scrollToPage", track, index);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module.IsValueCreated)
        {
            var module = await _module.Value;
            await module.DisposeAsync();
        }
    }

    private sealed class PageChangedCallback(Func<int, Task> onPageChanged)
    {
        [JSInvokable]
        public Task OnPageChanged(int index) => onPageChanged(index);
    }

    private sealed class Attachment(
        IJSObjectReference module,
        ElementReference track,
        DotNetObjectReference<PageChangedCallback> callback) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await module.InvokeVoidAsync("detach", track);
            }
            catch (JSDisconnectedException)
            {
                // The circuit/page is already gone, so there is nothing left to detach from.
            }

            callback.Dispose();
        }
    }
}
