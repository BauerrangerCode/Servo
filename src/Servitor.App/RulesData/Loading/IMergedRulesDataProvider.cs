using Servitor.RulesData.Merging;

namespace Servitor.App.RulesData.Loading;

/// <summary>
/// Owns which copy of the merged rules data the app works with. The copy in IndexedDB wins over the shipped
/// <c>merged/*.json</c> files unless the files are newer, so a merge run from inside the app is not silently
/// replaced by an older build-time result.
/// </summary>
public interface IMergedRulesDataProvider
{
    /// <summary>Raised after <see cref="StoreAsync"/> replaced the data in use.</summary>
    event Action? DataChanged;

    /// <returns>The merged data, or null when no merge has run yet and no merged files are shipped.</returns>
    Task<MergedRulesData?> GetAsync(CancellationToken cancellationToken = default);

    Task StoreAsync(MergedRulesData data, CancellationToken cancellationToken = default);
}
