using Servitor.App.RulesData.Loading;
using Servitor.App.RulesData.Merging;
using Servitor.App.Tests.RulesData.Loading;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.RulesData.Merging;

public class RulesDataMergeServiceTests
{
    private sealed class RecordingProvider : IMergedRulesDataProvider
    {
        public event Action? DataChanged;

        public List<MergedRulesData> Stored { get; } = [];

        public Task<MergedRulesData?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Stored.LastOrDefault());

        public Task StoreAsync(MergedRulesData data, CancellationToken cancellationToken = default)
        {
            Stored.Add(data);
            DataChanged?.Invoke();
            return Task.CompletedTask;
        }
    }

    private readonly StubHttpHandler _http = new();
    private readonly RecordingProvider _provider = new();

    private RulesDataMergeService CreateService() =>
        new(_http.CreateClient(), _provider, new FixedTimeProvider(new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task Merges_the_sources_fetched_over_http_and_stores_the_result()
    {
        _http.AddJson(
            "data/bsdata/wh40k-11e/Orks.json",
            "{\"catalogue\":{\"name\":\"Xenos - Orks\",\"sharedSelectionEntries\":[{\"name\":\"Boyz\",\"id\":\"u1\",\"type\":\"unit\"}]}}");

        var result = await CreateService().MergeAsync();

        Assert.True(result.Stored);
        Assert.Equal("Boyz", Assert.Single(Assert.Single(_provider.Stored).Datasheets).Name);
    }

    [Fact]
    public async Task Does_not_store_a_result_without_any_datasheets_so_working_data_is_never_wiped()
    {
        var result = await CreateService().MergeAsync();

        Assert.False(result.Stored);
        Assert.Empty(_provider.Stored);
        Assert.NotEmpty(result.Outcome.Report.Warnings);
    }

    [Fact]
    public async Task Forwards_progress_reports()
    {
        var reports = new System.Collections.Concurrent.ConcurrentQueue<MergeProgress>();

        await CreateService().MergeAsync(new ImmediateProgress(reports.Enqueue));

        Assert.NotEmpty(reports);
    }

    private sealed class ImmediateProgress(Action<MergeProgress> handler) : IProgress<MergeProgress>
    {
        public void Report(MergeProgress value) => handler(value);
    }
}
