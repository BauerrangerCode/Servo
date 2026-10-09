namespace Servitor.App.Scoring.History;

/// <summary>The scorecard history's persistence (E5.3): finished games survive a full app reload via <c>IRulesDataStore</c>.</summary>
public interface IScorecardStore
{
    /// <summary>All scorecards, newest game first.</summary>
    Task<IReadOnlyList<Scorecard>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Upserts by id: replaces a scorecard with the same id, or adds a new one.</summary>
    Task SaveAsync(Scorecard scorecard, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
