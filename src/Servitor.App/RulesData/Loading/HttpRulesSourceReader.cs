using Servitor.RulesData.Merging;

namespace Servitor.App.RulesData.Loading;

/// <summary>
/// Reads the raw source files the browser can fetch from <c>wwwroot/data/</c>. The dev server answers an unknown
/// path with the app's index.html (status 200), so a response that is not JSON counts as a missing file too.
/// Any error status is likewise reported as unreadable (null), so the merger records a warning instead of one bad
/// file aborting the whole merge.
/// </summary>
public sealed class HttpRulesSourceReader(HttpClient httpClient) : IRulesSourceReader
{
    public async Task<string?> ReadTextAsync(string relativePath, CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(RulesDataUrls.Get(RulesDataUrls.ForSourcePath(relativePath)), cancellationToken);
        if (!response.IsSuccessStatusCode || !RulesDataUrls.IsJson(response))
        {
            return null;
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
