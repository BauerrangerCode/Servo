using Microsoft.AspNetCore.Components;
using Servitor.App.Scoring.Tracking;

namespace Servitor.App.Scoring.Components;

/// <summary>Re-renders on every score change; needed because a child whose only parameter is an unchanged enum is otherwise not re-rendered with its parent.</summary>
public abstract class TrackingComponentBase : ComponentBase, IDisposable
{
    [Inject]
    protected IScoreTrackingState Tracking { get; set; } = default!;

    protected override void OnInitialized() => Tracking.Changed += OnTrackingChanged;

    public void Dispose() => Tracking.Changed -= OnTrackingChanged;

    private void OnTrackingChanged() => InvokeAsync(StateHasChanged);
}
