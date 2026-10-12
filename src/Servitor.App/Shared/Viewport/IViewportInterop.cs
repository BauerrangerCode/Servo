using Microsoft.AspNetCore.Components;

namespace Servitor.App.Shared.Viewport;

public interface IViewportInterop
{
    /// <summary>Scrolls a horizontally scrolling row so that its element with the "selected" class is centered.</summary>
    Task ScrollSelectedIntoViewAsync(ElementReference container);
}
