using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Servitor.App.RulesData.Loading;

internal static class RulesDataUrls
{
    private const string DataRoot = "data";
    private const string JsonMediaType = "application/json";

    public const string MergedManifest = $"{DataRoot}/merged/manifest.json";

    public const string MergedData = $"{DataRoot}/merged/rules-data.json";

    /// <summary>Source file names contain spaces, commas and apostrophes, so every path segment is escaped.</summary>
    public static string ForSourcePath(string relativePath) =>
        $"{DataRoot}/{string.Join('/', relativePath.Split('/').Select(Uri.EscapeDataString))}";

    /// <summary>
    /// A GET that revalidates with the server instead of trusting a heuristically cached copy, so a rebuilt merge
    /// result or a replaced source file is noticed on the next load.
    /// </summary>
    public static HttpRequestMessage Get(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.SetBrowserRequestCache(BrowserRequestCache.NoCache);
        return request;
    }

    public static bool IsJson(HttpResponseMessage response) =>
        response.Content.Headers.ContentType?.MediaType == JsonMediaType;
}
