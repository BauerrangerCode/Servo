namespace Servitor.App.Shared.Shell;

/// <summary>Closes the app when it runs inside the Servo launcher (the only place where closing from the page is possible).</summary>
public interface IAppShutdownService
{
    /// <summary>True when Servo serves this page, so <see cref="ShutdownAsync"/> can stop it.</summary>
    Task<bool> IsAvailableAsync();

    /// <summary>Asks Servo to close the window and stop. The window is closed by Servo, so the page is gone a moment after this returns.</summary>
    Task ShutdownAsync();
}
