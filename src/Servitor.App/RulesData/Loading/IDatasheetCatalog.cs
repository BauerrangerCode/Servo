using Servitor.RulesData.Domain;

namespace Servitor.App.RulesData.Loading;

/// <summary>
/// The in-memory, searchable pool of every datasheet of the merged rules data. Empty until a merge has produced
/// data (see <see cref="IMergedRulesDataProvider"/>).
/// </summary>
public interface IDatasheetCatalog
{
    /// <summary>Raised when a new merge result replaced the data behind <see cref="GetAllAsync"/>.</summary>
    event Action? Changed;

    Task<IReadOnlyList<Datasheet>> GetAllAsync(CancellationToken cancellationToken = default);
}
