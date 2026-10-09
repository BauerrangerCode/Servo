using System.Text.RegularExpressions;
using Servitor.RulesData.Merging;

namespace Servitor.DataUpdate.Updating;

public static partial class ManifestVersionPatcher
{
    /// <returns>True when the constant was found and now holds <paramref name="commit"/>.</returns>
    public static bool TryPatch(string sourceFile, string commit)
    {
        if (!File.Exists(sourceFile))
        {
            return false;
        }

        var text = File.ReadAllText(sourceFile);
        if (!VersionConstantRegex().IsMatch(text))
        {
            return false;
        }

        var patched = VersionConstantRegex().Replace(text, $"${{prefix}}{commit}${{suffix}}");
        if (patched != text)
        {
            File.WriteAllText(sourceFile, patched);
        }

        return true;
    }

    [GeneratedRegex(@"(?<prefix>public const string BsDataVersion = "")[^""]*(?<suffix>"";)")]
    private static partial Regex VersionConstantRegex();
}

/// <summary>Compares the source files on disk with the lists in <see cref="RulesSourceManifest"/>.</summary>
public static class ManifestDrift
{
    public static IEnumerable<string> Find(string dataRoot)
    {
        var bsData = Path.Combine(dataRoot, RulesSourceManifest.BsDataDirectory);
        var catalogues = Names(bsData, "*.json")
            .Where(n => !string.Equals(n, RulesSourceManifest.BsDataGameSystemFileName, StringComparison.OrdinalIgnoreCase));
        foreach (var message in Compare("BSData catalogue", catalogues, RulesSourceManifest.BsDataCatalogueFileNames))
        {
            yield return message;
        }

        var core = Path.Combine(dataRoot, RulesSourceManifest.CoreDirectory);
        var coreRules = Names(core, "*.json").Where(n => n != RulesSourceManifest.CoreStratagemsFileName);
        foreach (var message in Compare("core rules file", coreRules, RulesSourceManifest.CoreRuleFileNames))
        {
            yield return message;
        }

        var factionsRoot = Path.Combine(dataRoot, RulesSourceManifest.FactionsDirectory);
        var factions = Directory.Exists(factionsRoot)
            ? Directory.GetDirectories(factionsRoot)
                .Where(d => File.Exists(Path.Combine(d, RulesSourceManifest.FactionFileName)))
                .Select(d => Path.GetFileName(d)!)
            : [];
        foreach (var message in Compare("faction", factions, RulesSourceManifest.FactionSlugs))
        {
            yield return message;
        }
    }

    private static IEnumerable<string> Names(string directory, string pattern) =>
        Directory.Exists(directory) ? Directory.GetFiles(directory, pattern).Select(f => Path.GetFileName(f)!) : [];

    private static IEnumerable<string> Compare(string kind, IEnumerable<string> shipped, IReadOnlyList<string> listed)
    {
        var shippedSet = shipped.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in shippedSet.Except(listed, StringComparer.OrdinalIgnoreCase).Order())
        {
            yield return $"New {kind} '{name}' is missing from RulesSourceManifest and is ignored by the merge until it is added there.";
        }

        foreach (var name in listed.Except(shippedSet, StringComparer.OrdinalIgnoreCase).Order())
        {
            yield return $"RulesSourceManifest lists the {kind} '{name}', which no longer exists in the data.";
        }
    }
}
