using System.Net;
using Servitor.App.Shared.Shell;

namespace Servitor.App.Tests.Shared.Shell;

public class HttpAppShutdownServiceTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath));
            return Task.FromResult(respond(request));
        }
    }

    private HttpAppShutdownService CreateService(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

    private static HttpResponseMessage Text(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text) };

    [Fact]
    public async Task Servo_is_recognized_by_its_ping_answer()
    {
        Assert.True(await CreateService(new StubHandler(_ => Text("servo"))).IsAvailableAsync());
    }

    [Fact]
    public async Task Any_other_server_is_not_servo_even_when_it_answers_with_the_start_page()
    {
        Assert.False(await CreateService(new StubHandler(_ => Text("<html>app</html>"))).IsAvailableAsync());
    }

    [Fact]
    public async Task A_missing_ping_is_not_servo()
    {
        Assert.False(await CreateService(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))).IsAvailableAsync());
    }

    [Fact]
    public async Task Shutting_down_posts_the_quit_request()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        await CreateService(handler).ShutdownAsync();

        Assert.Equal((HttpMethod.Post, "/servo-quit"), Assert.Single(handler.Requests));
    }

    [Fact]
    public async Task A_refused_quit_request_is_an_error()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        await Assert.ThrowsAsync<HttpRequestException>(() => CreateService(handler).ShutdownAsync());
    }
}
