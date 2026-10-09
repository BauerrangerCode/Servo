using Servitor.App.RulesData.Loading;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.RulesData.Loading;

public class DatasheetCatalogTests
{
    private sealed class FakeProvider(MergedRulesData? data) : IMergedRulesDataProvider
    {
        public event Action? DataChanged;

        public Task<MergedRulesData?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(data);

        public Task StoreAsync(MergedRulesData newData, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void RaiseDataChanged() => DataChanged?.Invoke();
    }

    [Fact]
    public async Task Returns_the_datasheets_of_the_merged_data()
    {
        var catalog = new DatasheetCatalog(new FakeProvider(SampleMergedData.Create("v1", SampleMergedData.Older, "Boyz", "Nobz")));

        var sheets = await catalog.GetAllAsync();

        Assert.Equal(["Boyz", "Nobz"], sheets.Select(s => s.Name));
    }

    [Fact]
    public async Task Is_empty_while_no_merged_data_exists()
    {
        var catalog = new DatasheetCatalog(new FakeProvider(null));

        Assert.Empty(await catalog.GetAllAsync());
    }

    [Fact]
    public void Forwards_the_providers_data_changed_notification()
    {
        var provider = new FakeProvider(null);
        var catalog = new DatasheetCatalog(provider);
        var raised = 0;
        catalog.Changed += () => raised++;

        provider.RaiseDataChanged();

        Assert.Equal(1, raised);
    }
}
