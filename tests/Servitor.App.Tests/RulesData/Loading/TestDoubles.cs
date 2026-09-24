using System.Net;
using System.Text;
using Servitor.App.RulesData.Loading;
using Servitor.App.RulesData.Storage;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.RulesData.Loading;

internal sealed class InMemoryRulesDataStore : IRulesDataStore
{
    public Dictionary<string, object> Values { get; } = [];

    public int SaveCount { get; private set; }

    public Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        SaveCount++;
        Values[key] = value!;
        return Task.CompletedTask;
    }

    public Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(Values.TryGetValue(key, out var value) ? (T?)value : default);
}

/// <summary>Answers requests from a URL-to-response map; every unmapped URL behaves like the dev server's SPA fallback.</summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode Status, string Body, string MediaType)> _responses = [];

    public List<string> RequestedUrls { get; } = [];

    public bool ThrowNetworkError { get; set; }

    public void AddJson(string url, string body) => _responses[url] = (HttpStatusCode.OK, body, "application/json");

    public void AddStatus(string url, HttpStatusCode status) => _responses[url] = (status, "", "application/json");

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestedUrls.Add(request.RequestUri!.PathAndQuery.TrimStart('/'));
        if (ThrowNetworkError)
        {
            throw new HttpRequestException("offline");
        }

        var (status, body, mediaType) = _responses.GetValueOrDefault(
            RequestedUrls[^1], (HttpStatusCode.OK, "<html>index</html>", "text/html"));
        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        });
    }

    public HttpClient CreateClient() => new(this) { BaseAddress = new Uri("http://localhost/") };
}

internal static class SampleMergedData
{
    public static readonly DateTimeOffset Older = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset Newer = new(2026, 9, 10, 0, 0, 0, TimeSpan.Zero);

    public static Datasheet Sheet(string name) => new(
        name,
        "Orks",
        70,
        new StatLine("6\"", "3", "5+", null, "1", "7+", "2"),
        [],
        [],
        [],
        [],
        [],
        ["Infantry"],
        null,
        []);

    public static MergedRulesData Create(string version, DateTimeOffset generatedAt, params string[] datasheetNames) =>
        new(version, generatedAt, datasheetNames.Select(Sheet).ToList(), [], [], []);
}
