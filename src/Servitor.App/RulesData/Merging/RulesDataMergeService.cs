using Servitor.App.RulesData.Loading;
using Servitor.RulesData.Merging;

namespace Servitor.App.RulesData.Merging;

/// <summary>
/// The in-app twin of the build-time <c>Servitor.DataMerge</c> tool: the same <see cref="RulesDataMerger"/>, reading
/// the sources over HTTP and keeping the result in IndexedDB instead of writing files.
/// </summary>
public sealed class RulesDataMergeService(
    HttpClient httpClient, IMergedRulesDataProvider provider, TimeProvider timeProvider) : IRulesDataMergeService
{
    public async Task<InAppMergeResult> MergeAsync(
        IProgress<MergeProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var merger = new RulesDataMerger(new HttpRulesSourceReader(httpClient), timeProvider);
        var outcome = await merger.MergeAsync(progress, cancellationToken);

        if (outcome.Data.Datasheets.Count == 0)
        {
            return new InAppMergeResult(outcome, Stored: false);
        }

        await provider.StoreAsync(outcome.Data, cancellationToken);
        return new InAppMergeResult(outcome, Stored: true);
    }
}
