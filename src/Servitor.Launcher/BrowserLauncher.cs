using System.Diagnostics;

namespace Servitor.Launcher;

/// <summary>Opens the app in a full-screen app window of a Chromium-based browser.</summary>
public static class BrowserLauncher
{
    /// <summary>
    /// The app runs with a browser profile of its own, so it neither touches the user's normal browser nor depends on
    /// whether that one is already running — which is also what makes the started process live as long as the window.
    /// </summary>
    public static IReadOnlyList<string> Arguments(string appUrl, string profileDirectory) =>
    [
        $"--app={appUrl}",
        "--start-fullscreen",
        $"--user-data-dir={profileDirectory}",
        "--no-first-run",
        "--no-default-browser-check",
    ];

    /// <summary>Starts the browser; returns null when no supported browser is installed.</summary>
    public static Process? Start(string appUrl, string profileDirectory)
    {
        var browser = BrowserLocator.Find();
        if (browser is null)
        {
            return null;
        }

        Directory.CreateDirectory(profileDirectory);
        var startInfo = new ProcessStartInfo(browser) { UseShellExecute = false };
        foreach (var argument in Arguments(appUrl, profileDirectory))
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Process.Start(startInfo);
    }
}
