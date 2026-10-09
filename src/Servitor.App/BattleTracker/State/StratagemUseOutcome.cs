namespace Servitor.App.BattleTracker.State;

public enum StratagemUseResult
{
    Used,
    NotEnoughCommandPoints,
    AlreadyUsedThisPhase,
    TargetAlreadyTargeted,
}

/// <param name="Conflict">For <see cref="StratagemUseResult.TargetAlreadyTargeted"/>: the stratagem that already targeted the unit this phase.</param>
public sealed record StratagemUseOutcome(StratagemUseResult Result, string? Conflict = null)
{
    public bool IsUsed => Result == StratagemUseResult.Used;

    public string Reason => Result switch
    {
        StratagemUseResult.Used => "",
        StratagemUseResult.NotEnoughCommandPoints => "Not enough Command Points.",
        StratagemUseResult.AlreadyUsedThisPhase => "This stratagem was already used in this phase.",
        StratagemUseResult.TargetAlreadyTargeted => $"That unit was already targeted by {Conflict} in this phase.",
        _ => throw new ArgumentOutOfRangeException(nameof(Result), Result, null),
    };
}
