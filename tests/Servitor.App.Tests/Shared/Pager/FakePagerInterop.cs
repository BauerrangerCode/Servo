using Microsoft.AspNetCore.Components;
using Servitor.App.Shared.Pager;

namespace Servitor.App.Tests.Shared.Pager;

public sealed class FakePagerInterop : IPagerInterop
{
    private Func<int, Task>? _onPageChanged;

    public List<int> ScrolledTo { get; } = [];

    public int Detachments { get; private set; }

    public Task<IAsyncDisposable> AttachAsync(ElementReference track, Func<int, Task> onPageChanged)
    {
        _onPageChanged = onPageChanged;
        return Task.FromResult<IAsyncDisposable>(new Attachment(this));
    }

    public Task ScrollToAsync(ElementReference track, int index)
    {
        ScrolledTo.Add(index);
        return Task.CompletedTask;
    }

    public Task SwipeToAsync(int index) => _onPageChanged!(index);

    private sealed class Attachment(FakePagerInterop owner) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            owner.Detachments++;
            return ValueTask.CompletedTask;
        }
    }
}
