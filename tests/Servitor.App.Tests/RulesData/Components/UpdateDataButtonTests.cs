using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.RulesData.Components;
using Servitor.App.RulesData.Merging;
using Servitor.App.RulesData.Updating;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.RulesData.Components;

public class UpdateDataButtonTests : BunitContext
{
    private sealed class FakeUpdateService : IDataUpdateService
    {
        public bool Available { get; set; } = true;

        public DataUpdateResult Result { get; set; } = new(true, ["BSData: 46 catalogues"], ["New catalogue is missing from the manifest."]);

        public int Updates { get; private set; }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(Available);

        public Task<DataUpdateResult> UpdateAsync(IProgress<DataUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            Updates++;
            progress?.Report(new DataUpdateProgress("BSData", "Pulling"));
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeMergeService : IRulesDataMergeService
    {
        public int Merges { get; private set; }

        public bool Stored { get; set; } = true;

        public Task<InAppMergeResult> MergeAsync(IProgress<MergeProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            Merges++;
            var data = new MergedRulesData("v1", DateTimeOffset.UnixEpoch, [], [], [], []);
            var report = new MergeReport(
                "v1", data.GeneratedAtUtc, new MergeSummary(1519, 1519, 0, 0, 0, 1519, 28, 14, 22, 18, 1564, 49, 164, 1), [], [], [], [], [], [], [], []);
            return Task.FromResult(new InAppMergeResult(new MergeOutcome(data, report), Stored));
        }
    }

    private readonly FakeUpdateService _update = new();
    private readonly FakeMergeService _merge = new();

    public UpdateDataButtonTests()
    {
        Services.AddSingleton<IDataUpdateService>(_update);
        Services.AddSingleton<IRulesDataMergeService>(_merge);
    }

    [Fact]
    public async Task A_successful_update_shows_summary_and_warnings_and_merges_the_new_data()
    {
        var cut = Render<UpdateDataButton>();

        await cut.Find("button.update-data-button").ClickAsync();

        cut.WaitForAssertion(() => Assert.Contains("Update finished.", cut.Markup));
        Assert.Contains("BSData: 46 catalogues", cut.Find(".update-summary").TextContent);
        Assert.Contains("Merge: 1519 datasheets merged and stored.", cut.Find(".update-summary").TextContent);
        Assert.Contains("missing from the manifest", cut.Find(".update-warnings").TextContent);
        Assert.Equal(1, _merge.Merges);
    }

    [Fact]
    public async Task Progress_lines_stay_visible()
    {
        var cut = Render<UpdateDataButton>();

        await cut.Find("button.update-data-button").ClickAsync();

        cut.WaitForAssertion(() => Assert.Contains("BSData: Pulling", cut.Find(".update-progress").TextContent));
    }

    [Fact]
    public async Task A_missing_update_service_shows_how_to_start_it_and_does_nothing_else()
    {
        _update.Available = false;
        var cut = Render<UpdateDataButton>();

        await cut.Find("button.update-data-button").ClickAsync();

        Assert.Contains(HttpDataUpdateService.StartCommand, cut.Find(".update-message code").TextContent);
        Assert.Equal(0, _update.Updates);
        Assert.Equal(0, _merge.Merges);
        Assert.False(cut.Find("button.update-data-button").HasAttribute("disabled"));
    }

    [Fact]
    public async Task A_failed_update_is_shown_and_the_data_is_not_merged()
    {
        _update.Result = new DataUpdateResult(false, [], ["git is not installed or not on the PATH."]);
        var cut = Render<UpdateDataButton>();

        await cut.Find("button.update-data-button").ClickAsync();

        cut.WaitForAssertion(() => Assert.Contains("Update failed.", cut.Markup));
        Assert.Contains("git is not installed", cut.Find(".update-warnings").TextContent);
        Assert.Equal(0, _merge.Merges);
    }

    [Fact]
    public async Task A_merge_that_stores_nothing_is_reported()
    {
        _merge.Stored = false;
        var cut = Render<UpdateDataButton>();

        await cut.Find("button.update-data-button").ClickAsync();

        cut.WaitForAssertion(() => Assert.Contains("nothing was stored", cut.Find(".update-summary").TextContent));
    }
}
