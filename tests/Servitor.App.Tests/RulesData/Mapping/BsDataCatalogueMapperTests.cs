using Servitor.App.RulesData.Domain;
using Servitor.App.RulesData.Mapping;

namespace Servitor.App.Tests.RulesData.Mapping;

public class BsDataCatalogueMapperTests
{
    private static string ReadAstraMilitarumLibrary() =>
        File.ReadAllText(TestPaths.DataFile("bsdata", "wh40k-11e", "Imperium - Astra Militarum - Library.json"));

    [Fact]
    public void Maps_a_real_faction_catalogue_end_to_end_without_warnings()
    {
        var result = BsDataCatalogueMapper.Map(ReadAstraMilitarumLibrary());

        Assert.NotEmpty(result.Datasheets);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Maps_stat_line_and_ranged_weapon_for_a_known_unit()
    {
        var result = BsDataCatalogueMapper.Map(ReadAstraMilitarumLibrary());

        var shockTroops = Assert.Single(result.Datasheets, d => d.Name == "Cadian Shock Troops");

        Assert.Equal(70, shockTroops.Points);
        Assert.Equal("6\"", shockTroops.StatLine.Movement);
        Assert.Equal("3", shockTroops.StatLine.Toughness);
        Assert.Contains(shockTroops.RangedWeapons, w => w.Name == "Lasgun" && w.Keywords.Contains("Rapid Fire 1"));
    }

    [Fact]
    public void Reads_the_incursion_conditioned_modifier_for_the_same_datasheet_limit()
    {
        // Ground truth for docs/Open-Questions-Catalog.md Q9: Cadian Shock Troops (BATTLELINE) is capped at
        // 6 for Strike Force/Onslaught, with a modifier overriding that same constraint to 4 for Incursion.
        var result = BsDataCatalogueMapper.Map(ReadAstraMilitarumLibrary());

        var shockTroops = Assert.Single(result.Datasheets, d => d.Name == "Cadian Shock Troops");

        Assert.NotNull(shockTroops.CompositionConstraint);
        Assert.Equal(6, shockTroops.CompositionConstraint.StrikeForceAndOnslaughtLimit);
        Assert.Equal(4, shockTroops.CompositionConstraint.IncursionLimit);
    }

    [Fact]
    public void Invalid_json_is_a_handled_warning_not_an_exception()
    {
        var result = BsDataCatalogueMapper.Map("{ this is not valid json");

        Assert.Empty(result.Datasheets);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void A_unit_with_unexpected_missing_fields_is_skipped_with_a_warning_not_a_crash()
    {
        const string catalogueWithOneBrokenUnit = """
            {
              "catalogue": {
                "name": "Test Faction",
                "sharedSelectionEntries": [
                  { "id": "u1", "name": "Broken Unit", "type": "unit" }
                ],
                "sharedProfiles": []
              }
            }
            """;

        var result = BsDataCatalogueMapper.Map(catalogueWithOneBrokenUnit);

        // A unit with no costs/profiles/constraints is not malformed enough to fail — it should map to a
        // minimal datasheet (points 0, placeholder stat line) rather than being treated as an error.
        var datasheet = Assert.Single(result.Datasheets);
        Assert.Equal("Broken Unit", datasheet.Name);
        Assert.Equal(0, datasheet.Points);
        Assert.Empty(result.Warnings);
    }
}
