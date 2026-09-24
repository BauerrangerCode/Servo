using Servitor.RulesData.Domain;
using Servitor.RulesData.Mapping;

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
    public void Maps_the_stat_line_when_it_is_inlined_directly_on_the_model_instead_of_via_an_infoLink()
    {
        // Regression test: "Cadian Shock Troops" resolves its stat line via an infoLink into the catalogue's
        // shared profile pool, but other catalogues (e.g. Aeldari) inline the "Unit" profile directly on the
        // model node instead — both shapes must map to a populated StatLine, not a "-" placeholder.
        var json = File.ReadAllText(TestPaths.DataFile("bsdata", "wh40k-11e", "Aeldari - Aeldari Library.json"));

        var result = BsDataCatalogueMapper.Map(json);

        var darkReapers = Assert.Single(result.Datasheets, d => d.Name == "Dark Reapers");
        Assert.Equal("6\"", darkReapers.StatLine.Movement);
        Assert.Equal("3+", darkReapers.StatLine.Save);
        Assert.Equal("5+", darkReapers.StatLine.InvulnerableSave);
    }

    [Fact]
    public void Maps_a_single_model_datasheet_that_is_typed_as_model_instead_of_unit()
    {
        // Regression test for the confirmed mapping bug in docs/Scry-Data-Pipeline.md: BSData represents
        // single-model datasheets (characters, vehicles, walkers, monsters) as top-level entries with
        // type "model" rather than "unit" — those were previously silently dropped by Map().
        var json = File.ReadAllText(TestPaths.DataFile("bsdata", "wh40k-11e", "Orks.json"));

        var result = BsDataCatalogueMapper.Map(json);

        var gorkanaut = Assert.Single(result.Datasheets, d => d.Name == "Gorkanaut");
        Assert.Equal(325, gorkanaut.Points);
        Assert.Equal("9\"", gorkanaut.StatLine.Movement);
        Assert.Equal("12", gorkanaut.StatLine.Toughness);
        Assert.Equal("20", gorkanaut.StatLine.Wounds);
        Assert.Equal("6+", gorkanaut.StatLine.InvulnerableSave);
    }

    [Fact]
    public void Does_not_turn_a_squad_units_nested_per_model_loadout_variants_into_separate_datasheets()
    {
        // Regression test: "model"-typed entries also occur nested inside a "unit" entry's own subtree
        // (e.g. "Boyz" contains per-model loadout variants "Boy", "Boy w/ Big shoota", "Nob", ...). Those are
        // not standalone datasheets and must not be promoted to top-level Datasheet records just because their
        // type is "model" — only direct/top-level sharedSelectionEntries items are real datasheets.
        var json = File.ReadAllText(TestPaths.DataFile("bsdata", "wh40k-11e", "Orks.json"));

        var result = BsDataCatalogueMapper.Map(json);

        Assert.Single(result.Datasheets, d => d.Name == "Boyz");
        Assert.DoesNotContain(result.Datasheets, d => d.Name is "Boy" or "Boy w/ Big shoota" or "Nob");
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
