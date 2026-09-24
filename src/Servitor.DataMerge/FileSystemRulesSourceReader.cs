using Servitor.RulesData.Merging;

namespace Servitor.DataMerge;

internal sealed class FileSystemRulesSourceReader(string dataRoot) : IRulesSourceReader
{
    public async Task<string?> ReadTextAsync(string relativePath, CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(dataRoot, relativePath);
        return File.Exists(fullPath) ? await File.ReadAllTextAsync(fullPath, cancellationToken) : null;
    }
}
