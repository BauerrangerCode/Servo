using System.Net;
using System.Text;
using Servitor.App.Scoring.MissionDeck;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.Scoring.MissionDeck;

public class HttpMissionDeckProviderTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri!.PathAndQuery);
            return Task.FromResult(respond(request));
        }
    }

    private const string OneMission = """
        {
          "primaryMissions": [
            { "yourDisposition": "TakeAndHold", "opponentDisposition": "TakeAndHold", "name": "Battlefield Dominance", "legend": "", "scoringConditions": [] }
          ],
          "deploymentCards": []
        }
        """;

    [Fact]
    public async Task The_deck_file_is_fetched_and_parsed()
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(OneMission, Encoding.UTF8) });
        var provider = new HttpMissionDeckProvider(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

        Assert.Empty(provider.Current.Data.PrimaryMissions);
        await provider.LoadAsync();

        Assert.NotNull(provider.Current.Data.Resolve(MissionArchetype.TakeAndHold, MissionArchetype.TakeAndHold));
        Assert.Equal("/data/missions/mission-deck.json", handler.Urls.Single());
    }

    [Fact]
    public async Task A_file_that_cannot_be_fetched_leaves_no_missions_and_says_why()
    {
        var missing = new HttpMissionDeckProvider(new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))) { BaseAddress = new Uri("http://localhost/") });
        await missing.LoadAsync();
        Assert.Empty(missing.Current.Data.PrimaryMissions);
        Assert.Contains("404", missing.Current.Warnings.Single(), StringComparison.Ordinal);

        var offline = new HttpMissionDeckProvider(new HttpClient(new Handler(_ => throw new HttpRequestException("offline"))) { BaseAddress = new Uri("http://localhost/") });
        await offline.LoadAsync();
        Assert.Contains("offline", offline.Current.Warnings.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Loading_again_picks_up_a_changed_file()
    {
        var body = OneMission;
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        var provider = new HttpMissionDeckProvider(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });
        await provider.LoadAsync();

        body = "{\"primaryMissions\":[],\"deploymentCards\":[]}";
        await provider.LoadAsync();

        Assert.Empty(provider.Current.Data.PrimaryMissions);
    }
}
