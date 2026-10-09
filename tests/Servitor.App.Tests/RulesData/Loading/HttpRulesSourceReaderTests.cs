using System.Net;
using Servitor.App.RulesData.Loading;

namespace Servitor.App.Tests.RulesData.Loading;

public class HttpRulesSourceReaderTests
{
    private readonly StubHttpHandler _http = new();

    [Fact]
    public async Task Returns_the_file_text_for_a_json_response()
    {
        _http.AddJson("data/core/moving.json", "[]");

        var text = await new HttpRulesSourceReader(_http.CreateClient()).ReadTextAsync("core/moving.json", CancellationToken.None);

        Assert.Equal("[]", text);
    }

    [Fact]
    public async Task Escapes_spaces_commas_and_apostrophes_in_every_path_segment()
    {
        var reader = new HttpRulesSourceReader(_http.CreateClient());

        await reader.ReadTextAsync("bsdata/wh40k-11e/Warhammer 40,000.json", CancellationToken.None);
        await reader.ReadTextAsync("bsdata/wh40k-11e/T'au Empire.json", CancellationToken.None);

        Assert.Equal(
            ["data/bsdata/wh40k-11e/Warhammer%2040%2C000.json", "data/bsdata/wh40k-11e/T%27au%20Empire.json"],
            _http.RequestedUrls);
    }

    [Fact]
    public async Task A_404_means_the_file_does_not_exist()
    {
        _http.AddStatus("data/core/gone.json", HttpStatusCode.NotFound);

        var text = await new HttpRulesSourceReader(_http.CreateClient()).ReadTextAsync("core/gone.json", CancellationToken.None);

        Assert.Null(text);
    }

    [Fact]
    public async Task The_dev_servers_html_fallback_for_an_unknown_path_counts_as_missing()
    {
        var text = await new HttpRulesSourceReader(_http.CreateClient()).ReadTextAsync("core/unknown.json", CancellationToken.None);

        Assert.Null(text);
    }

    [Fact]
    public async Task A_server_error_counts_as_unreadable_so_one_bad_file_does_not_abort_the_merge()
    {
        _http.AddStatus("data/core/broken.json", HttpStatusCode.InternalServerError);

        var text = await new HttpRulesSourceReader(_http.CreateClient()).ReadTextAsync("core/broken.json", CancellationToken.None);

        Assert.Null(text);
    }
}
