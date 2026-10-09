using System.ComponentModel;
using Servitor.Launcher;

// Servo: one double-clickable program that serves the Servitor app locally and opens it as a full-screen app window.
// Closing that window stops Servo a few seconds later.

var options = ServoOptions.FromArguments(args);

try
{
    Directory.CreateDirectory(options.HomeDirectory);

    if (!File.Exists(Path.Combine(options.WebRoot, "index.html")))
    {
        Fail(options, $"The app files were not found in '{options.WebRoot}'. Keep the 'wwwroot' folder next to the Servo program.");
        return 1;
    }

    if (await IsServoRunningAsync(options))
    {
        // A second start only brings the existing window back (the browser hands over to the running instance).
        if (options.OpenBrowser)
        {
            BrowserLauncher.Start(options.AppUrl, options.BrowserProfileDirectory)?.Dispose();
        }

        return 0;
    }

    var monitor = new SessionMonitor(TimeProvider.System);
    await using var app = ServoHost.Build(options, monitor);
    await app.StartAsync();

    if (options.OpenBrowser)
    {
        // The browser process is not waited for (see SessionMonitor); the page tells Servo when it is closed.
        using var browser = BrowserLauncher.Start(options.AppUrl, options.BrowserProfileDirectory);
        if (browser is null)
        {
            Fail(options, "Servo needs Microsoft Edge, Google Chrome, Chromium or Brave to show the app in full screen, and none was found. Install one of them and start Servo again.");
            await app.StopAsync();
            return 1;
        }
    }

    var stopping = app.Lifetime.ApplicationStopping;
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
    try
    {
        while (await timer.WaitForNextTickAsync(stopping))
        {
            if (options.OpenBrowser && monitor.ShouldExit())
            {
                break;
            }
        }
    }
    catch (OperationCanceledException)
    {
        // Ctrl+C or a system shutdown: stop below like any other exit.
    }

    await app.StopAsync();
    return 0;
}
catch (Exception exception) when (exception is IOException or InvalidOperationException or HttpRequestException or Win32Exception)
{
    Fail(options, $"Servo could not start: {exception.Message}");
    return 1;
}

static async Task<bool> IsServoRunningAsync(ServoOptions options)
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
    try
    {
        var answer = await client.GetStringAsync($"{options.AppUrl}{ServoHost.PingPath}");
        return answer == ServoHost.PingAnswer;
    }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
    {
        return false;
    }
}

static void Fail(ServoOptions options, string message)
{
    try
    {
        File.AppendAllText(options.LogFile, $"{DateTime.Now:s} {message}{Environment.NewLine}");
    }
    catch (IOException)
    {
        // The home folder may be unwritable; the user still gets the message below.
    }

    UserMessage.Show("Servo", message);
}
