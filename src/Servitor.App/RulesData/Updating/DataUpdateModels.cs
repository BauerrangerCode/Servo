namespace Servitor.App.RulesData.Updating;

/// <summary>One line of the update service's progress stream.</summary>
public sealed record DataUpdateProgress(string Step, string Message);

/// <summary>The final line of the update service's stream. <paramref name="Summary"/> says what was updated, <paramref name="Warnings"/> what needs attention or why it failed.</summary>
public sealed record DataUpdateResult(bool Success, IReadOnlyList<string> Summary, IReadOnlyList<string> Warnings);
