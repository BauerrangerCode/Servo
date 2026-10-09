namespace Servitor.Launcher;

/// <summary>
/// Everything the launcher needs to know. <paramref name="WebRoot"/> is the published app, read-only and shipped next
/// to the executable; <paramref name="HomeDirectory"/> is the per-user folder for everything that is written
/// (rules data, the source clone, the browser profile, the log).
/// </summary>
public sealed record ServoOptions(
    IReadOnlyList<string> Urls,
    string AppUrl,
    string WebRoot,
    string HomeDirectory,
    bool OpenBrowser)
{
    public const int DefaultPort = 5187;
    public const int UpdateServicePort = 5180;

    public string DataDirectory => Path.Combine(HomeDirectory, "data");

    public string BsDataClone => Path.Combine(HomeDirectory, "sources", "bsdata", "wh40k-11e");

    public string BrowserProfileDirectory => Path.Combine(HomeDirectory, "browser-profile");

    public string LogFile => Path.Combine(HomeDirectory, "servo.log");

    /// <summary>
    /// The app origin has to stay the same between runs, because the browser stores armies and scores per origin, so
    /// the port is fixed. <c>--port</c>, <c>--web-root</c>, <c>--home</c> and <c>--no-browser</c> exist for tests and
    /// troubleshooting.
    /// </summary>
    public static ServoOptions FromArguments(string[] args)
    {
        var port = int.TryParse(ArgumentValue(args, "--port"), out var parsedPort) ? parsedPort : DefaultPort;
        var appUrl = $"http://localhost:{port}";
        var urls = new List<string> { appUrl };
        if (port != UpdateServicePort)
        {
            // The app calls the update service on this fixed port (HttpDataUpdateService.ServiceUrl).
            urls.Add($"http://localhost:{UpdateServicePort}");
        }

        return new ServoOptions(
            urls,
            appUrl,
            ArgumentValue(args, "--web-root") ?? Path.Combine(AppContext.BaseDirectory, "wwwroot"),
            ArgumentValue(args, "--home") ?? DefaultHomeDirectory(),
            OpenBrowser: !args.Contains("--no-browser"));
    }

    private static string DefaultHomeDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Servo");

    private static string? ArgumentValue(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
