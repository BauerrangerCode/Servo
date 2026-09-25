using Servitor.RulesData.Domain;
using Servitor.RulesData.Mapping;
using Servitor.RulesData.Mapping.RulesText;

namespace Servitor.App.Tests.RulesData.Mapping;

public class UnitSizeParserTests
{
    private static RtModelCost Row(string description, string cost, string? section = "YOUR UNIT COSTS") =>
        new() { Description = description, Cost = cost, Section = section };

    [Fact]
    public void Model_counts_and_points_become_sizes_ordered_by_model_count()
    {
        var sizes = UnitSizeParser.Parse([Row("10 models", "180"), Row("5 models", "90")]);

        Assert.Equal([new UnitSize(5, 90), new UnitSize(10, 180)], sizes);
    }

    [Fact]
    public void A_single_model_is_a_size_of_one()
    {
        Assert.Equal([new UnitSize(1, 80)], UnitSizeParser.Parse([Row("1 model", "80")]));
    }

    [Fact]
    public void Only_the_first_cost_bracket_is_read()
    {
        var sizes = UnitSizeParser.Parse(
        [
            Row("10 models", "90", "YOUR 1ST TO 2ND UNITS COST"),
            Row("10 models", "100", "YOUR 3RD + UNIT COSTS"),
        ]);

        Assert.Equal([new UnitSize(10, 90)], sizes);
    }

    [Fact]
    public void Wargear_rows_are_not_sizes()
    {
        var sizes = UnitSizeParser.Parse([Row("5 models", "70"), Row("per Lascannon", "20", "WARGEAR OPTIONS"), Row("2 models", "9", "WARGEAR OPTIONS")]);

        Assert.Equal([new UnitSize(5, 70)], sizes);
    }

    [Fact]
    public void A_mixed_composition_counts_all_its_models()
    {
        var sizes = UnitSizeParser.Parse([Row("3 Wolf Guard Headtakers, 3 Hunting Wolves", "100")]);

        Assert.Equal([new UnitSize(6, 100)], sizes);
    }

    [Fact]
    public void Repeated_sizes_and_rows_without_a_numeric_cost_or_count_are_skipped()
    {
        var sizes = UnitSizeParser.Parse([Row("5 models", "70"), Row("5 models", "75"), Row("10 models", ""), Row("Boyz", "20")]);

        Assert.Equal([new UnitSize(5, 70)], sizes);
    }

    [Fact]
    public void A_datasheet_without_cost_rows_has_no_sizes()
    {
        Assert.Empty(UnitSizeParser.Parse([]));
    }

    [Fact]
    public void Reading_a_real_datasheet_file_yields_its_sizes()
    {
        var json = File.ReadAllText(TestPaths.WwwrootDataFile("factions", "orks", "datasheets", "000000001.json"));

        var details = RulesTextMapper.MapDatasheetDetails(json);

        Assert.NotNull(details);
        Assert.NotEmpty(details.Sizes);
        Assert.All(details.Sizes, s => Assert.True(s.Models > 0 && s.Points > 0));
    }
}
