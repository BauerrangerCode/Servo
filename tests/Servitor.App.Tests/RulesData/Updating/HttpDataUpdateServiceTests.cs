using System.Net;
using System.Text;
using Servitor.App.RulesData.Updating;

namespace Servitor.App.Tests.RulesData.Updating;

public class HttpDataUpdateServiceTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8) };

    private static HttpDataUpdateService Service(Handler handler) => new(new HttpClient(handler));

    [Fact]
    public async Task The_service_is_available_when_the_status_endpoint_answers()
    {
        var handler = new Handler(_ => Ok("{}"));

        Assert.True(await Service(handler).IsAvailableAsync());
        Assert.Equal($"{HttpDataUpdateService.ServiceUrl}/api/status", handler.Requests.Single().RequestUri!.ToString());
    }

    [Fact]
    public async Task The_service_is_unavailable_when_the_connection_fails()
    {
        var handler = new Handler(_ => throw new HttpRequestException("refused"));

        Assert.False(await Service(handler).IsAvailableAsync());
    }

    [Fact]
    public async Task Progress_lines_are_reported_and_the_last_line_is_the_result()
    {
        var body =
            "{\"type\":\"progress\",\"step\":\"BSData\",\"message\":\"Pulling\"}\n"
            + "{\"type\":\"progress\",\"step\":\"Core rules\",\"message\":\"Scraping\"}\n"
            + "\n"
            + "{\"type\":\"result\",\"success\":true,\"summary\":[\"BSData: 46 catalogues\"],\"warnings\":[\"New file\"]}\n";
        var handler = new Handler(_ => Ok(body));
        var reported = new List<DataUpdateProgress>();

        var result = await Service(handler).UpdateAsync(new Progress<DataUpdateProgress>(reported.Add));
        await Task.Delay(50);

        Assert.True(result.Success);
        Assert.Equal(["BSData: 46 catalogues"], result.Summary);
        Assert.Equal(["New file"], result.Warnings);
        Assert.Equal([new DataUpdateProgress("BSData", "Pulling"), new DataUpdateProgress("Core rules", "Scraping")], reported);
        Assert.Equal(HttpMethod.Post, handler.Requests.Single().Method);
    }

    [Fact]
    public async Task A_failed_result_is_passed_on()
    {
        var handler = new Handler(_ => Ok("{\"type\":\"result\",\"success\":false,\"summary\":[],\"warnings\":[\"git is not installed\"]}\n"));

        var result = await Service(handler).UpdateAsync();

        Assert.False(result.Success);
        Assert.Equal(["git is not installed"], result.Warnings);
    }

    [Fact]
    public async Task A_stream_that_ends_without_a_result_counts_as_failed()
    {
        var handler = new Handler(_ => Ok("{\"type\":\"progress\",\"step\":\"BSData\",\"message\":\"Pulling\"}\n"));

        var result = await Service(handler).UpdateAsync();

        Assert.False(result.Success);
        Assert.Contains("closed the connection", result.Warnings.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_error_status_counts_as_failed_with_the_reason()
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent("An update is already running.") });

        var result = await Service(handler).UpdateAsync();

        Assert.False(result.Success);
        Assert.Contains("409", result.Warnings.Single(), StringComparison.Ordinal);
        Assert.Contains("already running", result.Warnings.Single(), StringComparison.Ordinal);
    }
}
