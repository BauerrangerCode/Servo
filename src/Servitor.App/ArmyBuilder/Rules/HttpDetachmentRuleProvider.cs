using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Servitor.App.ArmyBuilder.Rules;

/// <summary>Loads <c>wwwroot/rules/detachment-rules.json</c>. A file that cannot be fetched leaves the army without detachment rules and says so.</summary>
public sealed class HttpDetachmentRuleProvider(HttpClient httpClient) : IDetachmentRuleProvider
{
    public const string RulesUrl = "rules/detachment-rules.json";

    public DetachmentRuleSet Current { get; private set; } = DetachmentRuleSet.Empty;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        // Revalidate with the server so an edited rules file is noticed on the next load.
        using var request = new HttpRequestMessage(HttpMethod.Get, RulesUrl);
        request.SetBrowserRequestCache(BrowserRequestCache.NoCache);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            Current = response.IsSuccessStatusCode
                ? DetachmentRuleParser.Parse(await response.Content.ReadAsStringAsync(cancellationToken))
                : Unavailable($"the server answered {(int)response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            Current = Unavailable(ex.Message);
        }
    }

    private static DetachmentRuleSet Unavailable(string reason) =>
        new([], [$"The detachment rules file ({RulesUrl}) could not be loaded: {reason}."]);
}
