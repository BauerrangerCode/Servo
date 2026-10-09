namespace Servitor.App.Shared.Shell;

/// <summary>Talks to the Servo launcher that serves the page: <c>servo-ping</c> tells whether it is there, <c>servo-quit</c> makes it close the window and stop.</summary>
public sealed class HttpAppShutdownService(HttpClient httpClient) : IAppShutdownService
{
    public const string PingUrl = "servo-ping";
    public const string QuitUrl = "servo-quit";
    public const string PingAnswer = "servo";

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            // Any other host answers with the app's start page or an error, never with this word.
            return await httpClient.GetStringAsync(PingUrl) == PingAnswer;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task ShutdownAsync()
    {
        using var response = await httpClient.PostAsync(QuitUrl, content: null);
        response.EnsureSuccessStatusCode();
    }
}
