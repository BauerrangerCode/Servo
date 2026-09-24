using System.Text.Json;
using Servitor.DataMerge;
using Servitor.RulesData.Merging;

const int SuccessExitCode = 0;
const int FailureExitCode = 1;
const string OutputDirectoryName = "merged";
const string DataFileName = "rules-data.json";
const string ManifestFileName = "manifest.json";
const string ReportFileName = "merge-report.md";

var dataRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : FindDefaultDataRoot();
if (dataRoot is null || !Directory.Exists(dataRoot))
{
    Console.Error.WriteLine("Data root not found. Usage: Servitor.DataMerge [<path to wwwroot/data>]");
    return FailureExitCode;
}

var merger = new RulesDataMerger(new FileSystemRulesSourceReader(dataRoot), TimeProvider.System);
var outcome = await merger.MergeAsync();

if (outcome.Data.Datasheets.Count == 0)
{
    Console.Error.WriteLine($"Nothing to merge: no BSData datasheets found under '{dataRoot}'.");
    return FailureExitCode;
}

var outputDirectory = Path.Combine(dataRoot, OutputDirectoryName);
Directory.CreateDirectory(outputDirectory);

// The manifest is written last: a consumer that sees a manifest can rely on the data file it describes.
await File.WriteAllTextAsync(
    Path.Combine(outputDirectory, DataFileName),
    JsonSerializer.Serialize(outcome.Data, MergedDataJson.Options));
await File.WriteAllTextAsync(
    Path.Combine(outputDirectory, ReportFileName),
    MergeReportFormatter.ToMarkdown(outcome.Report));
await File.WriteAllTextAsync(
    Path.Combine(outputDirectory, ManifestFileName),
    JsonSerializer.Serialize(new MergedDataManifest(outcome.Data.DataVersion, outcome.Data.GeneratedAtUtc), MergedDataJson.Options));

var summary = outcome.Report.Summary;
Console.WriteLine($"Merged rules data {outcome.Data.DataVersion} -> {outputDirectory}");
Console.WriteLine(
    $"  datasheets: {summary.BsDataDatasheets} ({summary.MatchedDatasheets} matched, {summary.AmbiguousDatasheets} ambiguous, "
    + $"{summary.UnmatchedDatasheets} unmatched, {summary.UnmappedDatasheets} unmapped)");
Console.WriteLine(
    $"  stratagems: {summary.CoreStratagems} core + {summary.FactionStratagems} faction; "
    + $"core rules: {summary.CoreRulesFromBsData} BSData + {summary.CoreRulesAdded} added");
Console.WriteLine($"  warnings: {outcome.Report.Warnings.Count} (see {ReportFileName})");
return SuccessExitCode;

static string? FindDefaultDataRoot()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null)
    {
        var candidate = Path.Combine(directory.FullName, "src", "Servitor.App", "wwwroot", "data");
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        directory = directory.Parent;
    }

    return null;
}
