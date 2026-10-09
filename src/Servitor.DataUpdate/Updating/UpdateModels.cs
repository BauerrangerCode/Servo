namespace Servitor.DataUpdate.Updating;

/// <summary>
/// Where the update reads and writes. <paramref name="DataRoot"/> is <c>src/Servitor.App/wwwroot/data</c>,
/// <paramref name="BsDataRepository"/> the local clone of BSData/wh40k-11e (created on first run),
/// <paramref name="ManifestSourceFile"/> the C# file holding the BSData version constant (null: not patched).
/// </summary>
public sealed record UpdateOptions(string DataRoot, string BsDataRepository, string? ManifestSourceFile)
{
    public const string BsDataRepositoryUrl = "https://github.com/BSData/wh40k-11e.git";
}

/// <summary>One line of progress: which step is running and what it is doing.</summary>
public sealed record UpdateProgress(string Step, string Message);

/// <summary>The outcome of a whole update. <paramref name="Summary"/> lists what changed, <paramref name="Warnings"/> what needs attention.</summary>
public sealed record UpdateResult(bool Success, IReadOnlyList<string> Summary, IReadOnlyList<string> Warnings);
