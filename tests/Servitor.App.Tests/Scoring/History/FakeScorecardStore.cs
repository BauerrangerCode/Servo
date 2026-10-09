using Servitor.App.Scoring.History;

namespace Servitor.App.Tests.Scoring.History;

internal sealed class FakeScorecardStore : IScorecardStore
{
    private readonly List<Scorecard> _scorecards = [];

    public IReadOnlyList<Scorecard> Scorecards => _scorecards;

    public Exception? FailSavesWith { get; set; }

    public Task<IReadOnlyList<Scorecard>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Scorecard>>(_scorecards.OrderByDescending(s => s.EndedAtUtc).ToList());

    public Task SaveAsync(Scorecard scorecard, CancellationToken cancellationToken = default)
    {
        if (FailSavesWith is not null)
        {
            throw FailSavesWith;
        }

        _scorecards.RemoveAll(s => s.Id == scorecard.Id);
        _scorecards.Add(scorecard);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _scorecards.RemoveAll(s => s.Id == id);
        return Task.CompletedTask;
    }
}
