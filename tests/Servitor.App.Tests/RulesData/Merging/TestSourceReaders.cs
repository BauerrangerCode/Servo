using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.RulesData.Merging;

internal sealed class InMemoryRulesSourceReader(IReadOnlyDictionary<string, string> files) : IRulesSourceReader
{
    public Task<string?> ReadTextAsync(string relativePath, CancellationToken cancellationToken) =>
        Task.FromResult(files.GetValueOrDefault(relativePath));
}

internal sealed class DirectoryRulesSourceReader(string root) : IRulesSourceReader
{
    public async Task<string?> ReadTextAsync(string relativePath, CancellationToken cancellationToken)
    {
        var path = Path.Combine(root, relativePath);
        return File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken) : null;
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
