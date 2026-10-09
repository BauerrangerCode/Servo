namespace Servitor.Launcher;

/// <summary>
/// Finds an installed Chromium-based browser. Servo needs one because only these can open a page as a chrome-less,
/// full-screen app window with a profile of its own.
/// </summary>
public static class BrowserLocator
{
    public static string? Find() => Candidates().FirstOrDefault(File.Exists);

    /// <summary>The known install locations of the current platform, most preferred first.</summary>
    public static IEnumerable<string> Candidates()
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsCandidates();
        }

        if (OperatingSystem.IsMacOS())
        {
            return MacCandidates();
        }

        return LinuxCandidates();
    }

    private static IEnumerable<string> WindowsCandidates()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        }.Where(root => !string.IsNullOrEmpty(root)).ToList();

        // Edge ships with Windows, so it goes first.
        string[] relativePaths =
        [
            @"Microsoft\Edge\Application\msedge.exe",
            @"Google\Chrome\Application\chrome.exe",
            @"BraveSoftware\Brave-Browser\Application\brave.exe",
            @"Chromium\Application\chrome.exe",
        ];

        return relativePaths.SelectMany(relative => roots.Select(root => Path.Combine(root, relative)));
    }

    private static IEnumerable<string> MacCandidates()
    {
        string[] bundles =
        [
            "Google Chrome.app/Contents/MacOS/Google Chrome",
            "Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
            "Brave Browser.app/Contents/MacOS/Brave Browser",
            "Chromium.app/Contents/MacOS/Chromium",
        ];

        var userApplications = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications");
        return bundles.SelectMany(bundle => new[] { Path.Combine("/Applications", bundle), Path.Combine(userApplications, bundle) });
    }

    private static IEnumerable<string> LinuxCandidates()
    {
        string[] commands =
        [
            "google-chrome", "google-chrome-stable", "chromium", "chromium-browser",
            "microsoft-edge", "microsoft-edge-stable", "brave-browser",
        ];

        var directories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        return commands.SelectMany(command => directories.Select(directory => Path.Combine(directory, command)));
    }
}
