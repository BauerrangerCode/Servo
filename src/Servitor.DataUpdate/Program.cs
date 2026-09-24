using System.Text.Json;
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
builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy => policy
    .SetIsOriginAllowed(IsLocalOrigin)
    .AllowAnyHeader()
    .AllowAnyMethod()));
builder.Services.AddHttpClient(nameof(DataUpdater), client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Servitor-DataUpdate/0.1");
});

var app = builder.Build();
app.UseCors();

var gate = new SemaphoreSlim(1, 1);

app.MapGet("/api/status", () => Results.Json(new { ok = true, dataRoot = options.DataRoot, bsDataRepository = options.BsDataRepository }));

// Streams one JSON object per line: {"type":"progress",...} while running, then a final {"type":"result",...}.
app.MapPost("/api/update", async (HttpContext context, IHttpClientFactory httpClients) =>
{
    if (!await gate.WaitAsync(0))
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsync("An update is already running.");
        return;
    }

    try
    {
        context.Response.ContentType = "application/x-ndjson";
        var writeLock = new SemaphoreSlim(1, 1);

        async Task WriteLineAsync(object payload)
        {
            await writeLock.WaitAsync();
            try
            {
                await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions) + "\n");
                await context.Response.Body.FlushAsync();
            }
            finally
            {
                writeLock.Release();
            }
        }

        var updater = new DataUpdater(options, new GitClient(), httpClients.CreateClient(nameof(DataUpdater)));
        var progress = new Progress<UpdateProgress>(p => _ = WriteLineAsync(new { type = "progress", step = p.Step, message = p.Message }));
        var result = await updater.RunAsync(progress, context.RequestAborted);
        await WriteLineAsync(new { type = "result", success = result.Success, summary = result.Summary, warnings = result.Warnings });
    }
    finally
    {
        gate.Release();
    }
});

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

static bool IsLocalOrigin(string origin) =>
    Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback;

partial class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
}
