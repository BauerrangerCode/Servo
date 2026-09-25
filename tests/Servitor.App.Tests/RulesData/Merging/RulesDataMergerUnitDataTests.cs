using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.RulesData.Merging;

/// <summary>The unit data the merge adds for the army builder: the sizes of a unit and the armies that can take it.</summary>
public class RulesDataMergerUnitDataTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private static string Catalogue(string name, params string[] unitNames) =>
        "{\"catalogue\":{\"name\":\"" + name + "\",\"sharedSelectionEntries\":["
        + string.Join(",", unitNames.Select((n, i) => $"{{\"name\":\"{n}\",\"id\":\"u{i}\",\"type\":\"unit\"}}"))
        + "]}}";

    private static string Faction(string slug, params string[] datasheets) =>
        "{\"slug\":\"" + slug + "\",\"name\":\"" + slug + "\",\"dataVersion\":\"2026-01-01 00:00:00\",\"datasheets\":["
        + string.Join(",", datasheets.Select((n, i) => $"{{\"id\":\"{slug}-{i}\",\"name\":\"{n}\",\"isLegends\":false}}"))
        + "],\"detachments\":[]}";

    private static string DatasheetWithCosts(string id, params (string Description, string Cost, string Section)[] costs) =>
        "{\"id\":\"" + id + "\",\"stratagemIds\":[],\"modelCosts\":["
        + string.Join(",", costs.Select(c => $"{{\"description\":\"{c.Description}\",\"cost\":\"{c.Cost}\",\"section\":\"{c.Section}\"}}"))
        + "]}";

    private static Dictionary<string, string> Sources() => new()
    {
        ["bsdata/wh40k-11e/Orks.json"] = Catalogue("Xenos - Orks", "Boyz", "Squighog Boyz"),
        ["bsdata/wh40k-11e/Unaligned Forces.json"] = Catalogue("Unaligned Forces", "Stray"),
        ["factions/orks/faction.json"] = Faction("orks", "Boyz"),
        ["factions/genestealer-cults/faction.json"] = Faction("genestealer-cults", "Boyz"),
        ["factions/orks/datasheets/orks-0.json"] = DatasheetWithCosts(
            "orks-0",
            ("20 models", "180", "YOUR UNIT COSTS"),
            ("10 models", "90", "YOUR UNIT COSTS"),
            ("per Big shoota", "5", "WARGEAR OPTIONS")),
        ["factions/genestealer-cults/datasheets/genestealer-cults-0.json"] = DatasheetWithCosts("genestealer-cults-0", ("5 models", "50", "YOUR UNIT COSTS")),
    };

    private static async Task<MergeOutcome> MergeAsync() =>
        await new RulesDataMerger(new InMemoryRulesSourceReader(Sources()), new FixedTimeProvider(FixedNow)).MergeAsync();

    [Fact]
    public async Task A_matched_unit_gets_the_sizes_of_its_rules_text_datasheet_cheapest_first_without_wargear_rows()
    {
        var boyz = Assert.Single((await MergeAsync()).Data.Datasheets, d => d.Name == "Boyz");

        Assert.Equal([new UnitSize(10, 90), new UnitSize(20, 180)], boyz.Sizes);
    }

    [Fact]
    public async Task A_unit_the_rules_text_source_lists_in_several_armies_belongs_to_all_of_them()
    {
        var boyz = Assert.Single((await MergeAsync()).Data.Datasheets, d => d.Name == "Boyz");

        Assert.Equal(["genestealer-cults", "orks"], boyz.FactionSlugs.Order());
    }

    [Fact]
    public async Task An_unmatched_unit_falls_back_to_the_armies_of_its_catalogue_and_has_no_sizes()
    {
        var squighog = Assert.Single((await MergeAsync()).Data.Datasheets, d => d.Name == "Squighog Boyz");

        Assert.Equal(["orks"], squighog.FactionSlugs);
        Assert.Empty(squighog.Sizes);
    }

    [Fact]
    public async Task A_unit_of_an_unmapped_catalogue_belongs_to_no_army()
    {
        var stray = Assert.Single((await MergeAsync()).Data.Datasheets, d => d.Name == "Stray");

        Assert.Empty(stray.FactionSlugs);
    }
}
