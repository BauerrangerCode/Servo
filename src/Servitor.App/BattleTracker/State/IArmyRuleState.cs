namespace Servitor.App.BattleTracker.State;

/// <summary>
/// The player's choices around the Army Rules (E6.2): the auras toggle, pinned rules and the active alternative of a
/// rule that offers several. Persisted on every change, like the battle progress.
/// </summary>
public interface IArmyRuleState
{
    event Action? Changed;

    bool ShowAuras { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task SetShowAurasAsync(bool show);

    /// <summary>A pinned rule stays listed in every phase until it is unpinned, e.g. a target chosen "until the start of your next Command phase".</summary>
    bool IsPinned(string ruleId);

    string PinNote(string ruleId);

    Task PinAsync(string ruleId);

    Task UnpinAsync(string ruleId);

    Task SetPinNoteAsync(string ruleId, string note);

    string? ActiveOption(string ruleId);

    /// <summary>The alternatives that were active before and have been ended; shown as used, not blocked.</summary>
    IReadOnlyList<string> UsedOptions(string ruleId);

    /// <summary>Makes an alternative the active one; a previously active one is ended and counts as used.</summary>
    Task ActivateOptionAsync(string ruleId, string option);

    /// <summary>Ends the active alternative, which then counts as used.</summary>
    Task EndOptionAsync(string ruleId);
}
