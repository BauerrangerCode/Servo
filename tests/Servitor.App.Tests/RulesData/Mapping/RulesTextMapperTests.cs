using Servitor.App.RulesData.Domain;
using Servitor.App.RulesData.Mapping;

namespace Servitor.App.Tests.RulesData.Mapping;

public class RulesTextMapperTests
{
    [Fact]
    public void Maps_all_68_core_stratagems()
    {
        var json = File.ReadAllText(TestPaths.DataFile("core", "core-stratagems.json"));

        var result = RulesTextMapper.MapCoreStratagems(json);

        Assert.Equal(68, result.Stratagems.Count);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Parses_cp_cost_and_classifies_a_known_stratagem_category()
    {
        var json = File.ReadAllText(TestPaths.DataFile("core", "core-stratagems.json"));

        var result = RulesTextMapper.MapCoreStratagems(json);

        var allIn = Assert.Single(result.Stratagems, s => s.Name == "ALL IN");
        Assert.Equal(0, allIn.CpCost);
        Assert.Equal(StratagemCategory.StrategicPloy, allIn.Category);
    }

    [Fact]
    public void Sanitizes_html_in_stratagem_descriptions()
    {
        var json = File.ReadAllText(TestPaths.DataFile("core", "core-stratagems.json"));

        var result = RulesTextMapper.MapCoreStratagems(json);

        Assert.All(result.Stratagems, s => Assert.DoesNotContain("<script", s.DescriptionHtml, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Maps_a_real_factions_stratagems_abilities_and_enhancements()
    {
        var json = File.ReadAllText(TestPaths.DataFile("factions", "astra-militarum", "faction.json"));

        var result = RulesTextMapper.MapFaction(json);

        Assert.NotEmpty(result.Stratagems);
        Assert.NotEmpty(result.Abilities);
        Assert.NotEmpty(result.Enhancements);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Reads_the_upgrade_flag_on_an_upgrade_tagged_enhancement()
    {
        var json = File.ReadAllText(TestPaths.DataFile("factions", "astra-militarum", "faction.json"));

        var result = RulesTextMapper.MapFaction(json);

        Assert.Contains(result.Enhancements, e => e.IsUpgrade);
    }

    [Fact]
    public void Invalid_json_is_a_handled_warning_not_an_exception()
    {
        var result = RulesTextMapper.MapFaction("not json at all");

        Assert.Empty(result.Stratagems);
        Assert.NotEmpty(result.Warnings);
    }
}
