using System.Text.Json;
using Servitor.App.RulesData.Loading;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.RulesData.Loading;

public class MergedRulesDataProviderTests
{
    private const string ManifestUrl = "data/merged/manifest.json";
    private const string DataUrl = "data/merged/rules-data.json";
    private const string StoredManifestKey = "merged-rules-data/manifest";
    private const string StoredDataKey = "merged-rules-data/data";

    private readonly StubHttpHandler _http = new();
    private readonly InMemoryRulesDataStore _store = new();

    private MergedRulesDataProvider CreateProvider() => new(_http.CreateClient(), _store);

    private void Ship(MergedRulesData data)
    {
        _http.AddJson(ManifestUrl, JsonSerializer.Serialize(new MergedDataManifest(data.DataVersion, data.GeneratedAtUtc), MergedDataJson.Options));
        _http.AddJson(DataUrl, JsonSerializer.Serialize(data, MergedDataJson.Options));
    }

    private async Task StoreAsync(MergedRulesData data)
    {
        await _store.SaveAsync(StoredDataKey, data);
        await _store.SaveAsync(StoredManifestKey, new MergedDataManifest(data.DataVersion, data.GeneratedAtUtc));
    }

    [Fact]
    public async Task Loads_the_shipped_files_when_nothing_is_stored_and_keeps_a_copy_in_the_store()
    {
        Ship(SampleMergedData.Create("v1", SampleMergedData.Older, "Boyz"));

        var data = await CreateProvider().GetAsync();

        Assert.Equal("Boyz", Assert.Single(data!.Datasheets).Name);
        Assert.Contains(StoredDataKey, _store.Values.Keys);
        Assert.Contains(StoredManifestKey, _store.Values.Keys);
    }

    [Fact]
    public async Task Prefers_the_stored_copy_when_it_is_newer_than_the_shipped_files()
    {
        Ship(SampleMergedData.Create("v1", SampleMergedData.Older, "Shipped Boyz"));
        await StoreAsync(SampleMergedData.Create("v2", SampleMergedData.Newer, "Merged In App"));

        var data = await CreateProvider().GetAsync();

        Assert.Equal("Merged In App", Assert.Single(data!.Datasheets).Name);
        Assert.DoesNotContain(DataUrl, _http.RequestedUrls);
    }

    [Fact]
    public async Task Replaces_an_older_stored_copy_with_newer_shipped_files()
    {
        Ship(SampleMergedData.Create("v2", SampleMergedData.Newer, "Rebuilt Boyz"));
        await StoreAsync(SampleMergedData.Create("v1", SampleMergedData.Older, "Stale Boyz"));

        var data = await CreateProvider().GetAsync();

        Assert.Equal("Rebuilt Boyz", Assert.Single(data!.Datasheets).Name);
        Assert.Equal("v2", ((MergedDataManifest)_store.Values[StoredManifestKey]).DataVersion);
    }

    [Fact]
    public async Task Uses_the_stored_copy_when_no_files_are_shipped()
    {
        await StoreAsync(SampleMergedData.Create("v1", SampleMergedData.Older, "Stored Boyz"));

        var data = await CreateProvider().GetAsync();

        Assert.Equal("Stored Boyz", Assert.Single(data!.Datasheets).Name);
    }

    [Fact]
    public async Task Returns_null_when_nothing_is_stored_and_the_server_only_answers_with_the_app_shell()
    {
        var data = await CreateProvider().GetAsync();

        Assert.Null(data);
        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public async Task Treats_a_network_failure_as_no_shipped_files()
    {
        _http.ThrowNetworkError = true;
        await StoreAsync(SampleMergedData.Create("v1", SampleMergedData.Older, "Stored Boyz"));

        var data = await CreateProvider().GetAsync();

        Assert.Equal("Stored Boyz", Assert.Single(data!.Datasheets).Name);
    }

    [Fact]
    public async Task Storing_new_data_replaces_the_cache_and_notifies_listeners()
    {
        Ship(SampleMergedData.Create("v1", SampleMergedData.Older, "Shipped Boyz"));
        var provider = CreateProvider();
        await provider.GetAsync();
        var notifications = 0;
        provider.DataChanged += () => notifications++;

        await provider.StoreAsync(SampleMergedData.Create("v2", SampleMergedData.Newer, "Fresh Boyz"));
        var data = await provider.GetAsync();

        Assert.Equal("Fresh Boyz", Assert.Single(data!.Datasheets).Name);
        Assert.Equal(1, notifications);
        Assert.Equal("v2", ((MergedDataManifest)_store.Values[StoredManifestKey]).DataVersion);
    }

    [Fact]
    public async Task Fetches_the_shipped_files_only_once_per_session()
    {
        Ship(SampleMergedData.Create("v1", SampleMergedData.Older, "Boyz"));
        var provider = CreateProvider();

        await provider.GetAsync();
        await provider.GetAsync();

        Assert.Single(_http.RequestedUrls, url => url == DataUrl);
    }
}
