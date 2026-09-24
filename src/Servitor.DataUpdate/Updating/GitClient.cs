using System.Diagnostics;

namespace Servitor.DataUpdate.Updating;

/// <summary>The few git operations the BSData update needs. An interface so the update can be tested without git.</summary>
public interface IGitClient
{
    /// <summary>Clones the repository into <paramref name="directory"/> when it does not exist, otherwise fast-forwards it.</summary>
    Task UpdateAsync(string repositoryUrl, string directory, CancellationToken cancellationToken);

    /// <returns>The abbreviated hash of the checked-out commit.</returns>
    Task<string> GetShortCommitAsync(string directory, CancellationToken cancellationToken);
}

public sealed class GitClient : IGitClient
{
    public async Task UpdateAsync(string repositoryUrl, string directory, CancellationToken cancellationToken)
    {
        if (Directory.Exists(Path.Combine(directory, ".git")))
        {
            await RunAsync(["pull", "--ff-only"], directory, cancellationToken);
            return;
        }

        var parent = Path.GetDirectoryName(directory) ?? throw new InvalidOperationException($"'{directory}' has no parent directory.");
        Directory.CreateDirectory(parent);
        await RunAsync(["clone", "--depth", "1", repositoryUrl, directory], parent, cancellationToken);
    }

    public async Task<string> GetShortCommitAsync(string directory, CancellationToken cancellationToken) =>
        (await RunAsync(["rev-parse", "--short", "HEAD"], directory, cancellationToken)).Trim();

    private static async Task<string> RunAsync(string[] arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process process;
        try
        {
            process = Process.Start(startInfo) ?? throw new InvalidOperationException("git could not be started.");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException("git is not installed or not on the PATH.", ex);
        }

        using (process)
        {
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {(await error).Trim()}");
            }

            return await output;
        }
    }
}
