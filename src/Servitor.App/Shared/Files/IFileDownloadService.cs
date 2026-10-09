namespace Servitor.App.Shared.Files;

public interface IFileDownloadService
{
    Task DownloadTextAsync(string fileName, string content, string mimeType);
}
