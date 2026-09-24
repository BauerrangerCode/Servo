namespace Servitor.RulesData.Merging;

/// <summary>
/// Reads raw source files by path relative to the data root, so the same merge runs against the file system (build
/// tool) and against HTTP-served static files (browser).
/// </summary>
public interface IRulesSourceReader
{
    /// <returns>The file's text, or null when the file does not exist.</returns>
    Task<string?> ReadTextAsync(string relativePath, CancellationToken cancellationToken);
}
