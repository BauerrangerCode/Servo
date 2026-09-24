using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.RulesData.Merging;

public class RulesSourceManifestTests
{
    private static string BsDataFile(string fileName) =>
        TestPaths.WwwrootDataFile("bsdata", "wh40k-11e", fileName);

    [Fact]
    public void Every_bsdata_catalogue_in_the_manifest_ships_under_wwwroot()
    {
        foreach (var fileName in RulesSourceManifest.BsDataCatalogueFileNames)
        {
            Assert.True(File.Exists(BsDataFile(fileName)), $"Catalogue '{fileName}' is missing from wwwroot/data.");
        }

        Assert.True(File.Exists(BsDataFile(RulesSourceManifest.BsDataGameSystemFileName)));
    }

    [Fact]
    public void Every_bsdata_catalogue_shipped_under_wwwroot_is_in_the_manifest()
    {
        var shipped = Directory.GetFiles(BsDataFile(""), "*.json").Select(Path.GetFileName);
        var expected = RulesSourceManifest.BsDataCatalogueFileNames.Append(RulesSourceManifest.BsDataGameSystemFileName);

        Assert.Empty(shipped.Except(expected, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Core_rule_files_in_the_manifest_match_the_files_shipped_under_wwwroot()
    {
        var shipped = Directory.GetFiles(TestPaths.WwwrootDataFile("core"), "*.json")
            .Select(Path.GetFileName)
            .Where(name => name != RulesSourceManifest.CoreStratagemsFileName);

        Assert.Equal(
            RulesSourceManifest.CoreRuleFileNames.Order(StringComparer.Ordinal),
            shipped.Order(StringComparer.Ordinal));
        Assert.True(File.Exists(TestPaths.WwwrootDataFile("core", RulesSourceManifest.CoreStratagemsFileName)));
    }

    [Fact]
    public void Faction_slugs_in_the_manifest_match_the_faction_folders_that_hold_a_faction_file()
    {
        var shipped = Directory.GetDirectories(TestPaths.WwwrootDataFile("factions"))
            .Where(dir => File.Exists(Path.Combine(dir, RulesSourceManifest.FactionFileName)))
            .Select(dir => Path.GetFileName(dir)!);

        Assert.Equal(
            RulesSourceManifest.FactionSlugs.Order(StringComparer.Ordinal),
            shipped.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Manifest_lists_contain_no_duplicates()
    {
        Assert.Equal(
            RulesSourceManifest.BsDataCatalogueFileNames.Count,
            RulesSourceManifest.BsDataCatalogueFileNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(RulesSourceManifest.CoreRuleFileNames.Count, RulesSourceManifest.CoreRuleFileNames.Distinct().Count());
        Assert.Equal(RulesSourceManifest.FactionSlugs.Count, RulesSourceManifest.FactionSlugs.Distinct().Count());
    }

    [Fact]
    public void Faction_catalogue_map_only_names_real_catalogues_and_real_factions()
    {
        foreach (var catalogue in FactionCatalogueMap.MappedCatalogueFileNames)
        {
            Assert.Contains(catalogue, RulesSourceManifest.BsDataCatalogueFileNames);
            Assert.All(FactionCatalogueMap.FactionSlugsFor(catalogue), slug => Assert.Contains(slug, RulesSourceManifest.FactionSlugs));
        }
    }
}
