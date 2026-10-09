using Microsoft.JSInterop;

namespace Servitor.App.Shared.Files;

/// <summary>Wraps wwwroot/js/fileDownload.js so no component talks to JS interop directly.</summary>
public sealed class JsFileDownloadService(IJSRuntime jsRuntime) : IFileDownloadService, IAsyncDisposable
{
    private const string ModulePath = "./js/fileDownload.js";

    private readonly Lazy<Task<IJSObjectReference>> _module = new(() =>
        jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask());

    public async Task DownloadTextAsync(string fileName, string content, string mimeType)
    {
        var module = await _module.Value;
        await module.InvokeVoidAsync("downloadText", fileName, content, mimeType);
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
