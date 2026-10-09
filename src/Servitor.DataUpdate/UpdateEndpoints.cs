using System.Text.Json;
using Servitor.DataUpdate.Updating;

namespace Servitor.DataUpdate;

/// <summary>
/// The HTTP surface of the local update service. Shared by the stand-alone service (<c>Servitor.DataUpdate</c>) and the
/// Servo launcher, which hosts it next to the app so the "Update data" button works from the packaged app too.
/// </summary>
public static class UpdateEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Registers what the endpoints need: CORS for localhost pages only and the HTTP client of the updater.</summary>
    public static IServiceCollection AddUpdateServices(this IServiceCollection services)
    {
        services.AddCors(cors => cors.AddDefaultPolicy(policy => policy
            .SetIsOriginAllowed(IsLocalOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod()));
        services.AddHttpClient(nameof(DataUpdater), client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Servitor-DataUpdate/0.1");
        });
        return services;
    }

    /// <summary>Maps <c>/api/status</c> and <c>/api/update</c>. Call <c>UseCors()</c> before it.</summary>
    public static IEndpointRouteBuilder MapUpdateApi(this IEndpointRouteBuilder endpoints, UpdateOptions options)
    {
        var gate = new SemaphoreSlim(1, 1);

        endpoints.MapGet("/api/status", () => Results.Json(new { ok = true, dataRoot = options.DataRoot, bsDataRepository = options.BsDataRepository }));

        // Streams one JSON object per line: {"type":"progress",...} while running, then a final {"type":"result",...}.
        endpoints.MapPost("/api/update", async (HttpContext context, IHttpClientFactory httpClients) =>
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

        return endpoints;
    }

    private static bool IsLocalOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback;
}
