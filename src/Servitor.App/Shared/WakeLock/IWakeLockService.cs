namespace Servitor.App.Shared.WakeLock;

public interface IWakeLockService
{
    /// <summary>Keeps the screen awake until <see cref="ReleaseAsync"/>; false when the browser cannot or will not.</summary>
    Task<bool> AcquireAsync();

    Task ReleaseAsync();
}
