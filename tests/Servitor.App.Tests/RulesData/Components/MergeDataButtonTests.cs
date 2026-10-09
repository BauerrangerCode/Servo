using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.RulesData.Components;
using Servitor.App.RulesData.Merging;
using Servitor.App.Shared.Files;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.RulesData.Components;

public class MergeDataButtonTests : BunitContext
{
    private sealed class FakeMergeService : IRulesDataMergeService
    {
        public InAppMergeResult? Result { get; set; }

        public Exception? Failure { get; set; }

        public TaskCompletionSource? Gate { get; set; }

        public async Task<InAppMergeResult> MergeAsync(IProgress<MergeProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            progress?.Report(new MergeProgress("BSData catalogues", 25, 44));
            if (Gate is not null)
            {
                await Gate.Task;
            }

            return Failure is null ? Result! : throw Failure;
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

    private readonly FakeMergeService _mergeService = new();
    private readonly RecordingDownloads _downloads = new();

    public MergeDataButtonTests()
    {
        Services.AddSingleton<IRulesDataMergeService>(_mergeService);
        Services.AddSingleton<IFileDownloadService>(_downloads);
    }

    private static InAppMergeResult ResultWith(bool stored, int datasheets = 1519)
    {
        var data = new MergedRulesData("v1", new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero), [], [], [], []);
        var report = new MergeReport(
            "v1",
            data.GeneratedAtUtc,
            new MergeSummary(datasheets, datasheets, 0, 0, 0, datasheets, 28, 14, 22, 18, 1564, 49, 164, 1),
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            []);
        return new InAppMergeResult(new MergeOutcome(data, report), stored);
    }

    [Fact]
    public void Shows_a_merge_data_button_and_no_report_initially()
    {
        var cut = Render<MergeDataButton>();

        Assert.Equal("Merge data", cut.Find("button.merge-data-button").TextContent.Trim());
        Assert.Empty(cut.FindAll(".merge-report-dialog"));
    }

    [Fact]
    public async Task Clicking_the_button_runs_the_merge_and_shows_the_report()
    {
        _mergeService.Result = ResultWith(stored: true);
        var cut = Render<MergeDataButton>();

        await cut.Find("button.merge-data-button").ClickAsync();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".merge-report-dialog")));
        Assert.Contains("| BSData datasheets | 1519 |", cut.Find(".merge-report-text").TextContent);
        Assert.Empty(cut.FindAll(".merge-report-error"));
    }

    [Fact]
    public async Task While_the_merge_runs_the_button_is_disabled_and_shows_progress()
    {
        _mergeService.Result = ResultWith(stored: true);
        _mergeService.Gate = new TaskCompletionSource();
        var cut = Render<MergeDataButton>();

        var click = cut.Find("button.merge-data-button").ClickAsync();

        cut.WaitForAssertion(() =>
        {
            var button = cut.Find("button.merge-data-button");
            Assert.True(button.HasAttribute("disabled"));
            Assert.Equal("BSData catalogues 25/44", button.TextContent.Trim());
        });

        _mergeService.Gate.SetResult();
        await click;
        cut.WaitForAssertion(() => Assert.False(cut.Find("button.merge-data-button").HasAttribute("disabled")));
    }

    [Fact]
    public async Task Tells_the_user_when_nothing_was_stored_because_no_datasheets_were_found()
    {
        _mergeService.Result = ResultWith(stored: false, datasheets: 0);
        var cut = Render<MergeDataButton>();

        await cut.Find("button.merge-data-button").ClickAsync();

        cut.WaitForAssertion(() => Assert.Contains("nothing was stored", cut.Find(".merge-report-error").TextContent));
    }

    [Fact]
    public async Task Download_report_hands_the_markdown_to_the_download_service()
    {
        _mergeService.Result = ResultWith(stored: true);
        var cut = Render<MergeDataButton>();
        await cut.Find("button.merge-data-button").ClickAsync();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".merge-report-dialog")));

        await cut.FindAll(".merge-report-actions button").First(b => b.TextContent == "Download report").ClickAsync();

        var download = Assert.Single(_downloads.Downloads);
        Assert.Equal("merge-report.md", download.FileName);
        Assert.Equal("text/markdown", download.MimeType);
        Assert.StartsWith("# Rules data merge report", download.Content);
    }

    [Fact]
    public async Task Close_hides_the_report()
    {
        _mergeService.Result = ResultWith(stored: true);
        var cut = Render<MergeDataButton>();
        await cut.Find("button.merge-data-button").ClickAsync();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".merge-report-dialog")));

        await cut.FindAll(".merge-report-actions button").First(b => b.TextContent == "Close").ClickAsync();

        Assert.Empty(cut.FindAll(".merge-report-dialog"));
    }

    [Fact]
    public async Task A_failed_fetch_is_shown_as_an_error_instead_of_crashing_the_app()
    {
        _mergeService.Failure = new HttpRequestException("offline");
        var cut = Render<MergeDataButton>();

        await cut.Find("button.merge-data-button").ClickAsync();

        cut.WaitForAssertion(() => Assert.Contains("offline", cut.Find(".merge-report-error").TextContent));
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent == "Download report");
        Assert.False(cut.Find("button.merge-data-button").HasAttribute("disabled"));
    }
}
