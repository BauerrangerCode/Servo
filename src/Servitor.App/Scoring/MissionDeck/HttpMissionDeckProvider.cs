using System.Net;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Servitor.App.Scoring.MissionDeck;

/// <summary>Loads <c>wwwroot/data/missions/mission-deck.json</c>. A file that cannot be fetched leaves the deck empty and says so.</summary>
public sealed class HttpMissionDeckProvider(HttpClient httpClient) : IMissionDeckProvider
{
    public const string DeckUrl = "data/missions/mission-deck.json";
    private const string UpdateHint = " Use “Update data” in the settings menu to download it.";

    public MissionDeckSet Current { get; private set; } = MissionDeckSet.Empty;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        // Revalidate with the server so an edited deck file is noticed on the next load.
        using var request = new HttpRequestMessage(HttpMethod.Get, DeckUrl);
        request.SetBrowserRequestCache(BrowserRequestCache.NoCache);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            Current = response.IsSuccessStatusCode
                ? MissionDeckParser.Parse(await response.Content.ReadAsStringAsync(cancellationToken))
                : Unavailable($"the server answered {(int)response.StatusCode}", response.StatusCode == HttpStatusCode.NotFound);
        }
        catch (HttpRequestException ex)
        {
            Current = Unavailable(ex.Message);
        }
    }

    private static MissionDeckSet Unavailable(string reason, bool suggestUpdate = false) =>
        new(MissionDeckData.Empty, [$"The Mission Deck file ({DeckUrl}) could not be loaded: {reason}.{(suggestUpdate ? UpdateHint : "")}"]);
}
