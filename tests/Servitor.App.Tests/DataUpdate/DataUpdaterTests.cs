using System.Net;
using Servitor.DataUpdate.Updating;
using Servitor.DataUpdate.Wahapedia;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.DataUpdate;

public sealed class DataUpdaterTests : IDisposable
{
    private sealed class FakeGit(string commit) : IGitClient
    {
        public int Updates { get; private set; }

        public string? UpdatedUrl { get; private set; }

        public bool Fails { get; set; }

        public Task UpdateAsync(string repositoryUrl, string directory, CancellationToken cancellationToken)
        {
            if (Fails)
            {
                throw new InvalidOperationException("git is not installed or not on the PATH.");
            }

            Updates++;
            UpdatedUrl = repositoryUrl;
            Directory.CreateDirectory(directory);
            for (var i = 0; i < 12; i++)
            {
                File.WriteAllText(Path.Combine(directory, $"Catalogue {i}.json"), "{\"catalogue\":{}}");
            }

            File.WriteAllText(Path.Combine(directory, "README.md"), "not copied");
            return Task.CompletedTask;
        }

        public Task<string> GetShortCommitAsync(string directory, CancellationToken cancellationToken) => Task.FromResult(commit);
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), "servitor-updater-" + Guid.NewGuid().ToString("N"));
    private readonly StubHttpHandlerForUpdates _http = new();
    private readonly FakeGit _git = new("abc1234");

    private string DataRoot => Path.Combine(_root, "data");

    private DataUpdater CreateUpdater(string? manifestFile = null) =>
        new(new UpdateOptions(DataRoot, Path.Combine(_root, "bsdata-clone"), manifestFile), _git, new HttpClient(_http));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task A_successful_update_writes_bsdata_factions_core_stratagems_and_core_rules()
    {
        var result = await CreateUpdater().RunAsync(null, CancellationToken.None);

        Assert.True(result.Success, string.Join("; ", result.Warnings));
        Assert.Equal(12, Directory.GetFiles(Path.Combine(DataRoot, "bsdata", "wh40k-11e"), "*.json").Length);
        Assert.False(File.Exists(Path.Combine(DataRoot, "bsdata", "wh40k-11e", "README.md")));
        Assert.True(File.Exists(Path.Combine(DataRoot, "factions", "faction-00", "faction.json")));
        Assert.True(File.Exists(Path.Combine(DataRoot, "factions", "faction-00", "datasheets", "D00.json")));
        Assert.True(File.Exists(Path.Combine(DataRoot, "core", "core-stratagems.json")));
        Assert.True(File.Exists(Path.Combine(DataRoot, "core", "chapter-1.json")));
        Assert.Equal(UpdateOptions.BsDataRepositoryUrl, _git.UpdatedUrl);
        Assert.Contains(result.Summary, s => s.StartsWith("BSData: 12 catalogues at abc1234", StringComparison.Ordinal));
        Assert.Contains(result.Summary, s => s.StartsWith("Factions: 12 factions", StringComparison.Ordinal));
        Assert.Contains(result.Summary, s => s.StartsWith("Core rules: 12 chapters", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Progress_is_reported_per_step()
    {
        var steps = new List<string>();

        await CreateUpdater().RunAsync(new Progress<UpdateProgress>(p => steps.Add(p.Step)), CancellationToken.None);
        await Task.Delay(50);

        Assert.Contains("BSData", steps);
        Assert.Contains("Wahapedia data", steps);
        Assert.Contains("Core rules", steps);
        Assert.Contains("Manifest", steps);
    }

    [Fact]
    public async Task Stale_files_of_earlier_updates_are_removed_but_other_faction_data_is_kept()
    {
        Directory.CreateDirectory(Path.Combine(DataRoot, "factions", "faction-00", "datasheets"));
        File.WriteAllText(Path.Combine(DataRoot, "factions", "faction-00", "datasheets", "OLD.json"), "{}");
        Directory.CreateDirectory(Path.Combine(DataRoot, "factions", "unbound"));
        File.WriteAllText(Path.Combine(DataRoot, "factions", "unbound", "keep.txt"), "x");
        Directory.CreateDirectory(Path.Combine(DataRoot, "core"));
        File.WriteAllText(Path.Combine(DataRoot, "core", "removed-chapter.json"), "[]");
        Directory.CreateDirectory(Path.Combine(DataRoot, "bsdata", "wh40k-11e"));
        File.WriteAllText(Path.Combine(DataRoot, "bsdata", "wh40k-11e", "Gone.json"), "{}");

        await CreateUpdater().RunAsync(null, CancellationToken.None);

        Assert.False(File.Exists(Path.Combine(DataRoot, "factions", "faction-00", "datasheets", "OLD.json")));
        Assert.True(File.Exists(Path.Combine(DataRoot, "factions", "unbound", "keep.txt")));
        Assert.False(File.Exists(Path.Combine(DataRoot, "core", "removed-chapter.json")));
        Assert.False(File.Exists(Path.Combine(DataRoot, "bsdata", "wh40k-11e", "Gone.json")));
    }

    [Fact]
    public async Task Too_few_factions_fail_the_update_and_leave_existing_faction_files_alone()
    {
        _http.Tables = SampleWahapediaData.Tables(factionCount: 3);
        Directory.CreateDirectory(Path.Combine(DataRoot, "factions", "faction-00"));
        File.WriteAllText(Path.Combine(DataRoot, "factions", "faction-00", "faction.json"), "OLD");

        var result = await CreateUpdater().RunAsync(null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("only 3 factions", StringComparison.Ordinal));
        Assert.Equal("OLD", File.ReadAllText(Path.Combine(DataRoot, "factions", "faction-00", "faction.json")));
    }

    [Fact]
    public async Task A_changed_core_rules_page_fails_the_update_and_keeps_the_existing_core_files()
    {
        _http.CorePage = "<html><h1>Now completely different</h1></html>";
        Directory.CreateDirectory(Path.Combine(DataRoot, "core"));
        File.WriteAllText(Path.Combine(DataRoot, "core", "moving.json"), "OLD");

        var result = await CreateUpdater().RunAsync(null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("layout may have changed", StringComparison.Ordinal));
        Assert.Equal("OLD", File.ReadAllText(Path.Combine(DataRoot, "core", "moving.json")));
    }

    [Fact]
    public async Task A_failed_download_is_reported_not_thrown()
    {
        _http.Failing = true;

        var result = await CreateUpdater().RunAsync(null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public async Task A_missing_git_is_reported_not_thrown()
    {
        _git.Fails = true;

        var result = await CreateUpdater().RunAsync(null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("git is not installed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_new_bsdata_commit_is_written_into_the_manifest_source_and_drift_is_reported()
    {
        Directory.CreateDirectory(_root);
        var manifest = Path.Combine(_root, "RulesSourceManifest.cs");
        File.WriteAllText(manifest, "public const string BsDataVersion = \"8f5b01a\";");

        var result = await CreateUpdater(manifest).RunAsync(null, CancellationToken.None);

        Assert.Equal("public const string BsDataVersion = \"abc1234\";", File.ReadAllText(manifest));
        Assert.Contains(result.Summary, s => s.Contains("BsDataVersion set to abc1234", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.Contains("Catalogue 0.json", StringComparison.Ordinal) && w.Contains("missing from RulesSourceManifest", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_missing_manifest_source_is_reported_with_the_value_to_set()
    {
        var result = await CreateUpdater(Path.Combine(_root, "nope.cs")).RunAsync(null, CancellationToken.None);

        Assert.Contains(result.Warnings, w => w.Contains("set it to \"abc1234\"", StringComparison.Ordinal));
    }

    private sealed class StubHttpHandlerForUpdates : HttpMessageHandler
    {
        public Dictionary<string, string> Tables { get; set; } = SampleWahapediaData.Tables();

        public string CorePage { get; set; } = SampleWahapediaData.CorePage();

        public bool Failing { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Failing)
            {
                throw new HttpRequestException("offline");
            }

            var path = request.RequestUri!.AbsolutePath;
            var name = path[(path.LastIndexOf('/') + 1)..];
            var body = path.EndsWith("/core-rules/", StringComparison.Ordinal) ? CorePage : Tables.GetValueOrDefault(name);
            return Task.FromResult(body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}

public class ManifestDriftTests
{
    [Fact]
    public void The_version_constant_is_replaced_only_when_present()
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "x\npublic const string BsDataVersion = \"old\";\ny");
            Assert.True(ManifestVersionPatcher.TryPatch(file, "new1234"));
            Assert.Equal("x\npublic const string BsDataVersion = \"new1234\";\ny", File.ReadAllText(file));

            File.WriteAllText(file, "nothing here");
            Assert.False(ManifestVersionPatcher.TryPatch(file, "new1234"));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void The_shipped_data_matches_the_manifest_lists()
    {
        Assert.Empty(ManifestDrift.Find(TestPaths.WwwrootDataDirectory));
    }

    [Fact]
    public void Files_missing_from_or_extra_in_the_manifest_are_named()
    {
        var root = Path.Combine(Path.GetTempPath(), "servitor-drift-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "bsdata", "wh40k-11e"));
            File.WriteAllText(Path.Combine(root, "bsdata", "wh40k-11e", "Brand New.json"), "{}");
            Directory.CreateDirectory(Path.Combine(root, "factions", "orks"));
            File.WriteAllText(Path.Combine(root, "factions", "orks", "faction.json"), "{}");

            var messages = ManifestDrift.Find(root).ToList();

            Assert.Contains(messages, m => m.Contains("New BSData catalogue 'Brand New.json'", StringComparison.Ordinal));
            Assert.Contains(messages, m => m.Contains("lists the BSData catalogue 'Orks.json'", StringComparison.Ordinal));
            Assert.Contains(messages, m => m.Contains("lists the faction 'necrons'", StringComparison.Ordinal));
            Assert.DoesNotContain(messages, m => m.Contains("faction 'orks'", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
