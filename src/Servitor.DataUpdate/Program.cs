using Servitor.DataUpdate;
using Servitor.DataUpdate.Updating;

// Local update service for the Servitor app: the "Update data" button in the app's settings calls it, because a
// browser can neither run git nor write the data files. Listens on localhost only and accepts requests from
// localhost pages only.

const string DefaultUrl = "http://localhost:5180";
const string SolutionFileName = "Servitor.slnx";

var repoRoot = ArgumentValue(args, "--repo") ?? FindRepoRoot();
if (repoRoot is null)
{
    Console.Error.WriteLine($"Repository root not found (no {SolutionFileName} above the current directory). Pass --repo <path>.");
    return 1;
}

var options = new UpdateOptions(
    DataRoot: Path.Combine(repoRoot, "src", "Servitor.App", "wwwroot", "data"),
    BsDataRepository: ArgumentValue(args, "--bsdata") ?? Path.Combine(repoRoot, "data", "bsdata", "wh40k-11e"),
    ManifestSourceFile: Path.Combine(repoRoot, "src", "Servitor.RulesData", "Merging", "RulesSourceManifest.cs"));

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(ArgumentValue(args, "--urls") ?? DefaultUrl);
builder.Services.AddUpdateServices();

var app = builder.Build();
app.UseCors();
app.MapUpdateApi(options);

Console.WriteLine($"Servitor data update service on {ArgumentValue(args, "--urls") ?? DefaultUrl}");
Console.WriteLine($"  data root:        {options.DataRoot}");
Console.WriteLine($"  BSData repository: {options.BsDataRepository}");
await app.RunAsync();
return 0;

static string? ArgumentValue(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static string? FindRepoRoot()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    return null;
}
