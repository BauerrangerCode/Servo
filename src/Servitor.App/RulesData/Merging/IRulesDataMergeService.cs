using Servitor.RulesData.Merging;

namespace Servitor.App.RulesData.Merging;

/// <param name="Outcome">The merge result including its report.</param>
/// <param name="Stored">
/// False when the merge found no BSData datasheets at all (sources missing); the result then is not stored, so it
/// cannot replace working data with an empty set.
/// </param>
public sealed record InAppMergeResult(MergeOutcome Outcome, bool Stored);

public interface IRulesDataMergeService
{
    Task<InAppMergeResult> MergeAsync(IProgress<MergeProgress>? progress = null, CancellationToken cancellationToken = default);
}
