using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Servitor.Launcher;

/// <summary>
/// Tells the user something when Servo cannot start. The Windows build has no console window, and a double-clicked
/// program on the other systems has none either, so a plain console message would never be seen.
/// </summary>
public static partial class UserMessage
{
    private const uint MessageBoxIconError = 0x10;

    public static void Show(string title, string message)
    {
        Console.Error.WriteLine($"{title}: {message}");

        if (OperatingSystem.IsWindows())
        {
            MessageBoxW(IntPtr.Zero, message, title, MessageBoxIconError);
        }
        else if (OperatingSystem.IsMacOS())
        {
            RunDialogCommand("osascript", ["-e", $"display dialog \"{Escape(message)}\" with title \"{Escape(title)}\" buttons {{\"OK\"}} with icon stop"]);
        }
        else
        {
            RunDialogCommand("zenity", ["--error", $"--title={title}", $"--text={message}"]);
        }
    }

    private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // The dialog helpers are optional (zenity is not installed everywhere); the message is on stderr and in the log either way.
    private static void RunDialogCommand(string command, string[] arguments)
    {
        var startInfo = new ProcessStartInfo(command) { UseShellExecute = false };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            Process.Start(startInfo)?.WaitForExit();
        }
        catch (Win32Exception)
        {
            // Dialog helper not installed.
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBoxW(IntPtr window, string text, string caption, uint type);
}
