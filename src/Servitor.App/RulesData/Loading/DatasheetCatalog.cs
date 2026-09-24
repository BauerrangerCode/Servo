using Servitor.RulesData.Domain;

namespace Servitor.App.RulesData.Loading;

public sealed class DatasheetCatalog : IDatasheetCatalog
{
    private readonly IMergedRulesDataProvider _provider;

    public DatasheetCatalog(IMergedRulesDataProvider provider)
    {
        _provider = provider;
        _provider.DataChanged += () => Changed?.Invoke();
    }

    public event Action? Changed;

    public async Task<IReadOnlyList<Datasheet>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await _provider.GetAsync(cancellationToken))?.Datasheets ?? [];
}
