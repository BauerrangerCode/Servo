using System.Net;
using Microsoft.AspNetCore.Builder;
using Servitor.Launcher;

namespace Servitor.App.Tests.Launcher;

public sealed class ServoOptionsTests
{
    [Fact]
    public void DefaultsToTheFixedPortAndTheUpdateServicePort()
    {
        var options = ServoOptions.FromArguments([]);

        Assert.Equal("http://localhost:5187", options.AppUrl);
        Assert.Equal(["http://localhost:5187", "http://localhost:5180"], options.Urls);
        Assert.True(options.OpenBrowser);
    }

    [Fact]
    public void ReadsPortHomeWebRootAndNoBrowserFromTheArguments()
    {
        var options = ServoOptions.FromArguments(["--port", "6001", "--home", "h", "--web-root", "w", "--no-browser"]);

        Assert.Equal("http://localhost:6001", options.AppUrl);
        Assert.Equal("h", options.HomeDirectory);
        Assert.Equal("w", options.WebRoot);
        Assert.False(options.OpenBrowser);
    }

    [Fact]
    public void DoesNotListTheUpdateServicePortTwice()
    {
        var options = ServoOptions.FromArguments(["--port", "5180"]);

        Assert.Equal(["http://localhost:5180"], options.Urls);
    }
}

public sealed class BrowserLauncherTests
{
    [Fact]
    public void OpensAFullScreenAppWindowWithAProfileOfItsOwn()
    {
        var arguments = BrowserLauncher.Arguments("http://localhost:5187", "profile-dir");

        Assert.Contains("--app=http://localhost:5187", arguments);
        Assert.Contains("--start-fullscreen", arguments);
        Assert.Contains("--user-data-dir=profile-dir", arguments);
    }

    [Fact]
    public void KnowsAtLeastOneBrowserLocationOnThisSystem()
    {
        Assert.NotEmpty(BrowserLocator.Candidates());
    }
}

public sealed class ServoHostTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "servo-tests-" + Guid.NewGuid().ToString("N"));
    private SessionMonitor _monitor = null!;
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var webRoot = Path.Combine(_directory, "wwwroot");
        var home = Path.Combine(_directory, "home");
        Directory.CreateDirectory(webRoot);
        Directory.CreateDirectory(Path.Combine(home, "data"));
        File.WriteAllText(Path.Combine(webRoot, "index.html"), "<html>app</html>");
        File.WriteAllText(Path.Combine(webRoot, "blob.dat"), "binary");
        File.WriteAllText(Path.Combine(home, "data", "rules.json"), "{\"from\":\"home\"}");

        var options = new ServoOptions(["http://127.0.0.1:0"], "http://127.0.0.1:0", webRoot, home, OpenBrowser: false);
        _monitor = new SessionMonitor(TimeProvider.System);
        _app = ServoHost.Build(options, _monitor);
        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.Single()) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
        Directory.Delete(_directory, recursive: true);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/play")]
    [InlineData("/build")]
    public async Task ServesTheAppForTheStartPageAndForClientSideRoutes(string path)
    {
        var body = await _client.GetStringAsync(path);

        Assert.Equal("<html>app</html>", body);
    }

    [Fact]
    public async Task AnswersAMissingFileWithNotFoundInsteadOfTheApp()
    {
        var response = await _client.GetAsync("/assets/factions/missing.png");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ServesTheRulesDataFromTheHomeFolder()
    {
        var body = await _client.GetStringAsync("/data/rules.json");

        Assert.Equal("{\"from\":\"home\"}", body);
    }

    [Fact]
    public async Task ServesFilesWithAnUnknownExtension()
    {
        var response = await _client.GetAsync("/blob.dat");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AnswersThePingSoASecondStartCanRecognizeARunningServo()
    {
        var body = await _client.GetStringAsync(ServoHost.PingPath);

        Assert.Equal(ServoHost.PingAnswer, body);
    }

    [Fact]
    public async Task TheHeartbeatAndTheClosingReportReachTheMonitor()
    {
        Assert.Equal(HttpStatusCode.NoContent, (await _client.GetAsync(ServoHost.HeartbeatPath)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync(ServoHost.ClosingPath, null)).StatusCode);

        // Only the grace period after the closing report is left, so a later heartbeat has to cancel it again.
        await _client.GetAsync(ServoHost.HeartbeatPath);
        Assert.False(_monitor.ShouldExit());
    }

    [Fact]
    public async Task HostsTheUpdateApi()
    {
        var response = await _client.GetAsync("/api/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public sealed class SessionMonitorTests
{
    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan span) => _now += span;
    }

    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    [Fact]
    public void KeepsRunningWhileTheBrowserLoadsTheApp()
    {
        var time = new ManualTime();
        var monitor = new SessionMonitor(time);

        time.Advance(SessionMonitor.StartupTimeout - Second);

        Assert.False(monitor.ShouldExit());
    }

    [Fact]
    public void GivesUpWhenNoPageEverReportsIn()
    {
        var time = new ManualTime();
        var monitor = new SessionMonitor(time);

        time.Advance(SessionMonitor.StartupTimeout + Second);

        Assert.True(monitor.ShouldExit());
    }

    [Fact]
    public void KeepsRunningWhileHeartbeatsArrive()
    {
        var time = new ManualTime();
        var monitor = new SessionMonitor(time);

        for (var beat = 0; beat < 100; beat++)
        {
            monitor.Heartbeat();
            time.Advance(TimeSpan.FromSeconds(5));
        }

        Assert.False(monitor.ShouldExit());
    }

    [Fact]
    public void StopsShortlyAfterThePageReportsBeingClosed()
    {
        var time = new ManualTime();
        var monitor = new SessionMonitor(time);
        monitor.Heartbeat();

        monitor.Closing();
        time.Advance(SessionMonitor.CloseGrace - Second);
        Assert.False(monitor.ShouldExit());

        time.Advance(Second * 2);
        Assert.True(monitor.ShouldExit());
    }

    [Fact]
    public void AReloadedPageCancelsTheClosingReport()
    {
        var time = new ManualTime();
        var monitor = new SessionMonitor(time);
        monitor.Heartbeat();
        monitor.Closing();

        time.Advance(Second);
        monitor.Heartbeat();
        time.Advance(SessionMonitor.CloseGrace);

        Assert.False(monitor.ShouldExit());
    }

    [Fact]
    public void StopsWhenTheHeartbeatsStopWithoutAClosingReport()
    {
        var time = new ManualTime();
        var monitor = new SessionMonitor(time);
        monitor.Heartbeat();

        time.Advance(SessionMonitor.StaleAfter + Second);

        Assert.True(monitor.ShouldExit());
    }
}
