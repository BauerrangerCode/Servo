using System.ComponentModel;
using System.Diagnostics;

namespace Servitor.Launcher;

/// <summary>
/// Closes the app window. A page cannot do that itself (a browser only lets a script close windows it opened, and
/// the app window has navigated), and the process Servo started is not reliable either (see
/// <see cref="SessionMonitor"/>), so the browser is found by the profile folder only Servo's window uses.
/// </summary>
public static class BrowserCloser
{
    public static void Close(string profileDirectory)
    {
        var (command, arguments) = OperatingSystem.IsWindows()
            ? ("powershell", WindowsArguments(profileDirectory))
            : ("pkill", ["-f", "--", $"--user-data-dir={profileDirectory}"]);

        var startInfo = new ProcessStartInfo(command) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            Process.Start(startInfo)?.WaitForExit(TimeSpan.FromSeconds(20));
        }
        catch (Win32Exception)
        {
            // The tool is missing (pkill is not on every system); the window then stays open and the page says so.
        }
    }

    /// <summary>
    /// The main browser process (the one without <c>--type=</c>) is first asked to close its window, like the X
    /// button does. A fresh profile can have more windows than that one (first-run dialogs), and the browser only
    /// ends when all of them are gone, so whatever of the profile is still running after five seconds is stopped.
    /// </summary>
    public static string[] WindowsArguments(string profileDirectory)
    {
        var path = profileDirectory.Replace("'", "''");
        var script = $$"""
            $pattern = '*' + [WildcardPattern]::Escape('{{path}}') + '*'
            function Find-Browser { Get-CimInstance Win32_Process | Where-Object { $_.ProcessId -ne $PID -and $_.CommandLine -like $pattern } }
            Find-Browser | Where-Object { $_.CommandLine -notlike '*--type=*' } | ForEach-Object { (Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue).CloseMainWindow() | Out-Null }
            for ($i = 0; $i -lt 10 -and (Find-Browser); $i++) { Start-Sleep -Milliseconds 500 }
            Find-Browser | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
            """;
        return ["-NoProfile", "-NonInteractive", "-Command", script];
    }
}
