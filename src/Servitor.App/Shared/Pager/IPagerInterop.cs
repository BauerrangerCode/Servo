using Microsoft.AspNetCore.Components;

namespace Servitor.App.Shared.Pager;

public interface IPagerInterop
{
    /// <summary>Reports the page index whenever a swipe settles on another screen. Dispose the result to stop.</summary>
    Task<IAsyncDisposable> AttachAsync(ElementReference track, Func<int, Task> onPageChanged);

    Task ScrollToAsync(ElementReference track, int index);
}
