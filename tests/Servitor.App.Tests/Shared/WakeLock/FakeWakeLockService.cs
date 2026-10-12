using Servitor.App.Shared.WakeLock;

namespace Servitor.App.Tests.Shared.WakeLock;

public sealed class FakeWakeLockService : IWakeLockService
{
    public int Acquisitions { get; private set; }

    public int Releases { get; private set; }

    public Task<bool> AcquireAsync()
    {
        Acquisitions++;
        return Task.FromResult(true);
    }

    public Task ReleaseAsync()
    {
        Releases++;
        return Task.CompletedTask;
    }
}
