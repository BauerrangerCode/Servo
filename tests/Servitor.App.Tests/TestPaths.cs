namespace Servitor.App.Tests;

internal static class TestPaths
{
    /// <summary>
    /// Walks up from the test binary's output directory to find the repo root (marked by the top-level
    /// "data" directory, gitignored per CODING_GUIDELINES.md), so mapper tests can read real sample files.
    /// </summary>
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string DataFile(params string[] relativeSegments) =>
        Path.Combine([RepoRoot, "data", .. relativeSegments]);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "data", "bsdata")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate the repo's /data/bsdata directory above '{AppContext.BaseDirectory}'.");
    }
}
