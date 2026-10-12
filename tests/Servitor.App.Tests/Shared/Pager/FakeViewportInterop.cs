using Microsoft.AspNetCore.Components;
using Servitor.App.Shared.Viewport;

namespace Servitor.App.Tests.Shared.Pager;

public sealed class FakeViewportInterop : IViewportInterop
{
    public int Centerings { get; private set; }

    public Task ScrollSelectedIntoViewAsync(ElementReference container)
    {
        Centerings++;
        return Task.CompletedTask;
    }
}
