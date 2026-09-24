namespace Servitor.App.RulesData.Updating;

/// <summary>
/// Talks to the local update service (<c>Servitor.DataUpdate</c>) that pulls BSData, downloads the Wahapedia data and
/// rewrites the source files under <c>wwwroot/data</c> — things a browser cannot do itself.
/// </summary>
public interface IDataUpdateService
{
    /// <returns>True when the update service answers on this machine.</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <returns>The final result, or a failed one when the service ended the stream without one.</returns>
    Task<DataUpdateResult> UpdateAsync(IProgress<DataUpdateProgress>? progress = null, CancellationToken cancellationToken = default);
}
