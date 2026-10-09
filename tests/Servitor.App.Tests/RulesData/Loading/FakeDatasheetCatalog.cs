using Servitor.App.RulesData.Loading;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.RulesData.Loading;

internal sealed class FakeDatasheetCatalog(IReadOnlyList<Datasheet> datasheets) : IDatasheetCatalog
{
    private IReadOnlyList<Datasheet> _datasheets = datasheets;

    public FakeDatasheetCatalog()
        : this([])
    {
    }

    public event Action? Changed;

    public Task<IReadOnlyList<Datasheet>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_datasheets);

    public void Replace(IReadOnlyList<Datasheet> datasheets)
    {
        _datasheets = datasheets;
        Changed?.Invoke();
    }
}
