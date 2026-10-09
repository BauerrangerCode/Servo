using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Servitor.App.RulesData.Updating;

public sealed class HttpDataUpdateService(HttpClient httpClient) : IDataUpdateService
{
    public const string ServiceUrl = "http://localhost:5180";
    public const string StartCommand = "dotnet run --project src/Servitor.DataUpdate";

    private const string ProgressType = "progress";
    private const string ResultType = "result";

    private static readonly TimeSpan AvailabilityTimeout = TimeSpan.FromSeconds(3);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(AvailabilityTimeout);
        try
        {
            using var response = await httpClient.GetAsync($"{ServiceUrl}/api/status", timeout.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public async Task<DataUpdateResult> UpdateAsync(IProgress<DataUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ServiceUrl}/api/update");
        request.SetBrowserResponseStreamingEnabled(true);

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Failed($"The update service answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            switch (root.GetProperty("type").GetString())
            {
                case ProgressType:
                    progress?.Report(new DataUpdateProgress(root.GetProperty("step").GetString() ?? "", root.GetProperty("message").GetString() ?? ""));
                    break;
                case ResultType:
                    return root.Deserialize<DataUpdateResult>(JsonOptions) ?? Failed("The update service sent an unreadable result.");
            }
        }

        return Failed("The update service closed the connection before it finished.");
    }

    private static DataUpdateResult Failed(string reason) => new(false, [], [reason]);
}
