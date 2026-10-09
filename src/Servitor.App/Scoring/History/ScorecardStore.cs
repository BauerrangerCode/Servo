using Servitor.App.RulesData.Storage;

namespace Servitor.App.Scoring.History;

public sealed class ScorecardStore(IRulesDataStore store) : IScorecardStore
{
    private const string Key = "scoring/scorecards";

    public async Task<IReadOnlyList<Scorecard>> ListAsync(CancellationToken cancellationToken = default) =>
        (await store.LoadAsync<List<Scorecard>>(Key, cancellationToken) ?? []).OrderByDescending(s => s.EndedAtUtc).ToList();

    public async Task SaveAsync(Scorecard scorecard, CancellationToken cancellationToken = default)
    {
        var scorecards = (await ListAsync(cancellationToken)).Where(s => s.Id != scorecard.Id).ToList();
        scorecards.Add(scorecard);
        await store.SaveAsync(Key, scorecards, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var scorecards = (await ListAsync(cancellationToken)).Where(s => s.Id != id).ToList();
        await store.SaveAsync(Key, scorecards, cancellationToken);
    }
}
