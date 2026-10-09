using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Servitor.DataUpdate;
using Servitor.DataUpdate.Updating;

namespace Servitor.Launcher;

/// <summary>
/// The local web server behind Servo: serves the published app, serves the rules data from the user's home folder
/// under <c>/data</c> (so the update service can write it), and hosts the update API.
/// </summary>
public static class ServoHost
{
    public const string PingPath = "/servo-ping";
    public const string PingAnswer = "servo";
    public const string HeartbeatPath = "/servo-heartbeat";
    public const string ClosingPath = "/servo-closing";

    public static WebApplication Build(ServoOptions options, SessionMonitor monitor)
    {
        Directory.CreateDirectory(options.DataDirectory);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = options.WebRoot,
            WebRootPath = options.WebRoot,
        });
        builder.WebHost.UseUrls([.. options.Urls]);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddUpdateServices();

        var app = builder.Build();
        app.UseCors();

        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings[".webmanifest"] = "application/manifest+json";

        // Registered first so /data is answered from the writable home folder, never from the read-only app folder.
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(options.DataDirectory),
            RequestPath = "/data",
            ContentTypeProvider = contentTypes,
        });
        app.UseDefaultFiles();

        // The app ships files with extensions the type provider does not know (e.g. the .dat globalization data).
        app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = contentTypes,
            ServeUnknownFileTypes = true,
            DefaultContentType = "application/octet-stream",
        });

        app.MapGet(PingPath, () => PingAnswer);
        app.MapGet(HeartbeatPath, () =>
        {
            monitor.Heartbeat();
            return Results.NoContent();
        });
        app.MapPost(ClosingPath, () =>
        {
            monitor.Closing();
            return Results.NoContent();
        });
        app.MapUpdateApi(new UpdateOptions(
            DataRoot: options.DataDirectory,
            BsDataRepository: options.BsDataClone,
            ManifestSourceFile: null));

        // Client-side routes (/play, /build, ...) fall back to the app; a missing file (has an extension) stays a 404.
        app.MapFallbackToFile("{*path:nonfile}", "index.html");

        return app;
    }
}
