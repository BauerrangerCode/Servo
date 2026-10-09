using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.RulesData.Components;
using Servitor.App.RulesData.Merging;
using Servitor.App.RulesData.Updating;
using Servitor.App.Shared.Files;
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

        public Exception? Failure { get; set; }

        public TaskCompletionSource? Gate { get; set; }

        public async Task<InAppMergeResult> MergeAsync(IProgress<MergeProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            Merges++;
            progress?.Report(new MergeProgress("BSData catalogues", 25, 44));
            if (Gate is not null)
            {
                await Gate.Task;
            }

            if (Failure is not null)
            {
                throw Failure;
            }

            var data = new MergedRulesData("v1", DateTimeOffset.UnixEpoch, [], [], [], []);
            var report = new MergeReport(
                "v1", data.GeneratedAtUtc, new MergeSummary(1519, 1519, 0, 0, 0, 1519, 28, 14, 22, 18, 1564, 49, 164, 1), [], [], [], [], [], [], [], []);
            return new InAppMergeResult(new MergeOutcome(data, report), Stored);
        }
    }

    private sealed class RecordingDownloads : IFileDownloadService
    {
        public List<(string FileName, string Content, string MimeType)> Downloads { get; } = [];

        public Task DownloadTextAsync(string fileName, string content, string mimeType)
        {
            Downloads.Add((fileName, content, mimeType));
            return Task.CompletedTask;
        }
    }

    private readonly FakeUpdateService _update = new();
    private readonly FakeMergeService _merge = new();
    private readonly RecordingDownloads _downloads = new();

    public UpdateDataButtonTests()
    {
        Services.AddSingleton<IDataUpdateService>(_update);
        Services.AddSingleton<IRulesDataMergeService>(_merge);
        Services.AddSingleton<IFileDownloadService>(_downloads);
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
    public async Task While_the_merge_runs_the_button_is_disabled_and_shows_the_merge_progress()
    {
        _merge.Gate = new TaskCompletionSource();
        var cut = Render<UpdateDataButton>();

        var click = cut.Find("button.update-data-button").ClickAsync();

        cut.WaitForAssertion(() =>
        {
            var button = cut.Find("button.update-data-button");
            Assert.True(button.HasAttribute("disabled"));
            Assert.Equal("BSData catalogues 25/44", button.TextContent.Trim());
        });

        _merge.Gate.SetResult();
        await click;
        cut.WaitForAssertion(() =>
        {
            Assert.False(cut.Find("button.update-data-button").HasAttribute("disabled"));
            Assert.Equal("Update data", cut.Find("button.update-data-button").TextContent.Trim());
        });
    }

    [Fact]
    public async Task Without_the_update_service_it_says_so_and_merges_the_data_already_present()
    {
        _update.Available = false;
        var cut = Render<UpdateDataButton>();

        await cut.Find("button.update-data-button").ClickAsync();

        cut.WaitForAssertion(() => Assert.Contains("Merge: 1519 datasheets merged and stored.", cut.Find(".update-summary").TextContent));
        Assert.Contains(HttpDataUpdateService.StartCommand, cut.Find(".update-message code").TextContent);
        Assert.Equal(0, _update.Updates);
        Assert.Equal(1, _merge.Merges);
        Assert.DoesNotContain("Update finished.", cut.Markup);
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
        Assert.Empty(cut.FindAll("button.merge-report-open"));
    }

    [Fact]
    public async Task A_merge_that_stores_nothing_is_reported()
    {
        _merge.Stored = false;
        var cut = Render<UpdateDataButton>();

        await cut.Find("button.update-data-button").ClickAsync();

        cut.WaitForAssertion(() => Assert.Contains("nothing was stored", cut.Find(".update-summary").TextContent));
    }

    [Fact]
    public async Task A_failed_merge_is_shown_instead_of_crashing_the_app()
    {
        _merge.Failure = new HttpRequestException("offline");
        var cut = Render<UpdateDataButton>();

        await cut.Find("button.update-data-button").ClickAsync();

        cut.WaitForAssertion(() => Assert.Contains("Merge failed: offline", cut.Find(".update-summary").TextContent));
        Assert.Empty(cut.FindAll("button.merge-report-open"));
        Assert.False(cut.Find("button.update-data-button").HasAttribute("disabled"));
    }

    [Fact]
    public async Task The_merge_report_opens_on_request_and_closes_again()
    {
        var cut = Render<UpdateDataButton>();
        Assert.Empty(cut.FindAll("button.merge-report-open"));
        await cut.Find("button.update-data-button").ClickAsync();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("button.merge-report-open")));
        Assert.Empty(cut.FindAll(".merge-report-dialog"));

        await cut.Find("button.merge-report-open").ClickAsync();

        Assert.Contains("| BSData datasheets | 1519 |", cut.Find(".merge-report-text").TextContent);
        Assert.Empty(cut.FindAll(".merge-report-error"));

        await cut.FindAll(".merge-report-actions button").First(b => b.TextContent == "Close").ClickAsync();

        Assert.Empty(cut.FindAll(".merge-report-dialog"));
    }

    [Fact]
    public async Task The_merge_report_can_be_downloaded()
    {
        var cut = Render<UpdateDataButton>();
        await cut.Find("button.update-data-button").ClickAsync();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("button.merge-report-open")));
        await cut.Find("button.merge-report-open").ClickAsync();

        await cut.FindAll(".merge-report-actions button").First(b => b.TextContent == "Download report").ClickAsync();

        var download = Assert.Single(_downloads.Downloads);
        Assert.Equal("merge-report.md", download.FileName);
        Assert.Equal("text/markdown", download.MimeType);
        Assert.StartsWith("# Rules data merge report", download.Content);
    }

    [Fact]
    public async Task The_report_of_a_merge_that_stored_nothing_says_so()
    {
        _merge.Stored = false;
        var cut = Render<UpdateDataButton>();
        await cut.Find("button.update-data-button").ClickAsync();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("button.merge-report-open")));

        await cut.Find("button.merge-report-open").ClickAsync();

        Assert.Contains("nothing was stored", cut.Find(".merge-report-error").TextContent);
    }
}
