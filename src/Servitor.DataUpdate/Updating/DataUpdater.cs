using System.Text.Json;
using Servitor.DataUpdate.CoreRules;
using Servitor.DataUpdate.MissionDeck;
using Servitor.DataUpdate.Wahapedia;
using Servitor.RulesData.Merging;

namespace Servitor.DataUpdate.Updating;

/// <summary>
/// Refreshes the raw rules sources under <c>wwwroot/data</c>: the BSData catalogues, the faction / datasheet /
/// core-stratagem files generated from the Wahapedia CSV exports (what depot used to produce), the core-rules
/// text scraped from the Wahapedia rules page and the Mission Deck scraped from its page. Everything is fetched and validated before a source's files are
/// touched, so a failed download or a changed page format leaves the existing data as it was.
/// </summary>
public sealed class DataUpdater(UpdateOptions options, IGitClient git, HttpClient http)
{
    private const int MinimumBsDataCatalogues = 10;
    private const int MinimumFactions = 10;
    private const int MinimumCoreFiles = 10;
    private const string CoreRulesUrl = WahapediaTables.BaseUrl + "the-rules/core-rules/";
    private const string CoreStratagemsFileName = "core-stratagems.json";
    private const int MinimumPrimaryMissions = 15;
    private const int MinimumDeploymentCards = 4;
    private const int MinimumSecondaryMissions = 10;
    private const string MissionDeckUrl = WahapediaTables.BaseUrl + "the-rules/mission-deck-2026-27/";
    private const string MissionsDirectory = "missions";
    private const string MissionDeckFileName = "mission-deck.json";

    private string? _bsDataCommit;

    public async Task<UpdateResult> RunAsync(IProgress<UpdateProgress>? progress, CancellationToken cancellationToken)
    {
        var summary = new List<string>();
        var warnings = new List<string>();

        try
        {
            summary.Add(await UpdateBsDataAsync(progress, cancellationToken));
            summary.Add(await UpdateFactionsAsync(progress, cancellationToken));
            summary.Add(await UpdateCoreRulesAsync(progress, cancellationToken));
            summary.Add(await UpdateMissionDeckAsync(progress, warnings, cancellationToken));
            warnings.AddRange(SyncManifest(progress, summary));
            return new UpdateResult(true, summary, warnings);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or IOException or JsonException)
        {
            warnings.Add(ex.Message);
            return new UpdateResult(false, summary, warnings);
        }
    }

    // ---- BSData ------------------------------------------------------------------------------------------------

    private async Task<string> UpdateBsDataAsync(IProgress<UpdateProgress>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(new UpdateProgress("BSData", "Pulling the BSData repository"));
        await git.UpdateAsync(UpdateOptions.BsDataRepositoryUrl, options.BsDataRepository, cancellationToken);
        var commit = await git.GetShortCommitAsync(options.BsDataRepository, cancellationToken);

        var sources = Directory.GetFiles(options.BsDataRepository, "*.json", SearchOption.TopDirectoryOnly);
        if (sources.Length < MinimumBsDataCatalogues)
        {
            throw new InvalidOperationException($"The BSData repository holds only {sources.Length} JSON files; expected at least {MinimumBsDataCatalogues}.");
        }

        progress?.Report(new UpdateProgress("BSData", $"Copying {sources.Length} catalogues"));
        var target = Path.Combine(options.DataRoot, RulesSourceManifest.BsDataDirectory);
        Directory.CreateDirectory(target);
        foreach (var stale in Directory.GetFiles(target, "*.json"))
        {
            File.Delete(stale);
        }

        foreach (var source in sources)
        {
            File.Copy(source, Path.Combine(target, Path.GetFileName(source)), overwrite: true);
        }

        _bsDataCommit = commit;
        return $"BSData: {sources.Length} catalogues at {commit}";
    }

    // ---- Wahapedia CSV -> factions / datasheets / core stratagems ----------------------------------------------

    private async Task<string> UpdateFactionsAsync(IProgress<UpdateProgress>? progress, CancellationToken cancellationToken)
    {
        var csv = new Dictionary<string, string>();
        var done = 0;
        foreach (var fileName in WahapediaTables.FileNames)
        {
            progress?.Report(new UpdateProgress("Wahapedia data", $"Downloading {fileName} ({++done}/{WahapediaTables.FileNames.Count})"));
            csv[fileName] = await http.GetStringAsync(WahapediaTables.BaseUrl + fileName, cancellationToken);
        }

        progress?.Report(new UpdateProgress("Wahapedia data", "Converting tables"));
        var generated = DepotDataGenerator.Generate(WahapediaTables.Parse(csv));
        if (generated.Factions.Count < MinimumFactions)
        {
            throw new InvalidOperationException($"The Wahapedia export produced only {generated.Factions.Count} factions; expected at least {MinimumFactions}.");
        }

        progress?.Report(new UpdateProgress("Wahapedia data", $"Writing {generated.Factions.Count} factions"));
        var factionsRoot = Path.Combine(options.DataRoot, RulesSourceManifest.FactionsDirectory);
        foreach (var faction in generated.Factions)
        {
            var directory = Path.Combine(factionsRoot, faction.Slug);
            var datasheets = Path.Combine(directory, "datasheets");
            if (Directory.Exists(datasheets))
            {
                Directory.Delete(datasheets, recursive: true);
            }

            Directory.CreateDirectory(datasheets);
            await File.WriteAllTextAsync(Path.Combine(directory, RulesSourceManifest.FactionFileName), Json(faction.Manifest), cancellationToken);
            foreach (var (id, datasheet) in faction.Datasheets)
            {
                await File.WriteAllTextAsync(Path.Combine(datasheets, id + ".json"), Json(datasheet), cancellationToken);
            }
        }

        var core = Path.Combine(options.DataRoot, RulesSourceManifest.CoreDirectory);
        Directory.CreateDirectory(core);
        await File.WriteAllTextAsync(Path.Combine(core, CoreStratagemsFileName), Json(generated.CoreStratagems), cancellationToken);

        var datasheetCount = generated.Factions.Sum(f => f.Datasheets.Count);
        return $"Factions: {generated.Factions.Count} factions, {datasheetCount} datasheets, {generated.CoreStratagems.Count} core stratagem entries (data {generated.DataVersion})";
    }

    private static string Json(System.Text.Json.Nodes.JsonNode node) => node.ToJsonString(JsonOptions);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // ---- core rules page -> core/*.json ------------------------------------------------------------------------

    private async Task<string> UpdateCoreRulesAsync(IProgress<UpdateProgress>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(new UpdateProgress("Core rules", "Downloading the core rules page"));
        var page = await http.GetStringAsync(CoreRulesUrl, cancellationToken);

        progress?.Report(new UpdateProgress("Core rules", "Splitting the page into chapters"));
        var files = CoreRulesScraper.Scrape(page);
        if (files.Count < MinimumCoreFiles)
        {
            throw new InvalidOperationException($"The core rules page produced only {files.Count} chapters; expected at least {MinimumCoreFiles}. Its layout may have changed.");
        }

        var directory = Path.Combine(options.DataRoot, RulesSourceManifest.CoreDirectory);
        Directory.CreateDirectory(directory);
        var written = files.Select(f => f.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var stale in Directory.GetFiles(directory, "*.json").Where(f => Path.GetFileName(f) != CoreStratagemsFileName))
        {
            if (!written.Contains(Path.GetFileName(stale)))
            {
                File.Delete(stale);
            }
        }

        foreach (var file in files)
        {
            await File.WriteAllTextAsync(Path.Combine(directory, file.FileName), file.ToJson(), cancellationToken);
        }

        return $"Core rules: {files.Count} chapters, {files.Sum(f => f.Entries.Count)} sections";
    }

    // ---- mission deck page -> missions/mission-deck.json -------------------------------------------------------------

    private async Task<string> UpdateMissionDeckAsync(IProgress<UpdateProgress>? progress, List<string> warnings, CancellationToken cancellationToken)
    {
        progress?.Report(new UpdateProgress("Mission deck", "Downloading the Mission Deck page"));
        var page = await http.GetStringAsync(MissionDeckUrl, cancellationToken);

        progress?.Report(new UpdateProgress("Mission deck", "Reading the mission cards"));
        var deck = MissionDeckScraper.Scrape(page, DateOnly.FromDateTime(DateTime.UtcNow));
        if (deck.PrimaryMissions < MinimumPrimaryMissions || deck.DeploymentCards < MinimumDeploymentCards || deck.SecondaryMissions < MinimumSecondaryMissions)
        {
            throw new InvalidOperationException(
                $"The Mission Deck page produced {deck.PrimaryMissions} primary missions, {deck.DeploymentCards} deployment cards and {deck.SecondaryMissions} secondary missions; "
                + $"expected at least {MinimumPrimaryMissions}, {MinimumDeploymentCards} and {MinimumSecondaryMissions}. Its layout may have changed.");
        }

        var directory = Path.Combine(options.DataRoot, MissionsDirectory);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, MissionDeckFileName), deck.Json, cancellationToken);

        warnings.AddRange(deck.Warnings.Select(w => $"Mission deck: {w}"));
        return $"Mission deck: {deck.PrimaryMissions} primary missions, {deck.DeploymentCards} deployment cards, {deck.SecondaryMissions} secondary missions";
    }

    // ---- manifest ----------------------------------------------------------------------------------------------

    /// <summary>
    /// Writes the new BSData commit into the version constant of <see cref="RulesSourceManifest"/> and reports every
    /// shipped source file the manifest's hand-kept lists (which the browser needs, as it cannot list a folder) do not match.
    /// </summary>
    private List<string> SyncManifest(IProgress<UpdateProgress>? progress, List<string> summary)
    {
        progress?.Report(new UpdateProgress("Manifest", "Checking the source manifest"));
        var warnings = new List<string>();

        if (_bsDataCommit is not null && options.ManifestSourceFile is not null)
        {
            if (ManifestVersionPatcher.TryPatch(options.ManifestSourceFile, _bsDataCommit))
            {
                summary.Add($"Manifest: BsDataVersion set to {_bsDataCommit}");
            }
            else
            {
                warnings.Add($"Could not update BsDataVersion in '{options.ManifestSourceFile}'; set it to \"{_bsDataCommit}\" by hand.");
            }
        }

        warnings.AddRange(ManifestDrift.Find(options.DataRoot));
        return warnings;
    }
}
