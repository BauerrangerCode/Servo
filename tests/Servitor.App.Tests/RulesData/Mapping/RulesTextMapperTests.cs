using Servitor.RulesData.Domain;
using Servitor.RulesData.Mapping;

namespace Servitor.App.Tests.RulesData.Mapping;

public class RulesTextMapperTests
{
    private static string CoreFile(string name) => File.ReadAllText(TestPaths.WwwrootDataFile("core", name));

    private static string FactionFile(string slug) =>
        File.ReadAllText(TestPaths.WwwrootDataFile("factions", slug, "faction.json"));

    [Fact]
    public void Splits_the_68_core_entries_into_stratagems_with_cp_and_cost_free_actions()
    {
        var result = RulesTextMapper.MapCoreStratagems(CoreFile("core-stratagems.json"));

        Assert.Equal(38, result.Stratagems.Count);
        Assert.Equal(30, result.Actions.Count);
        Assert.All(result.Stratagems, s => Assert.NotNull(s.CpCost));
        Assert.All(result.Actions, a => Assert.Equal(CoreRuleKind.Action, a.Kind));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Maps_an_action_with_a_readable_name_and_markdown_text()
    {
        var result = RulesTextMapper.MapCoreStratagems(CoreFile("core-stratagems.json"));

        var advance = result.Actions.First(a => a.Name == "Advance Move");
        Assert.Contains("**ELIGIBLE IF:**", advance.Markdown);
        Assert.DoesNotMatch("<[a-zA-Z/]", advance.Markdown);
        Assert.Contains(result.Actions, a => a.Name == "Close-Quarters Shooting");
    }

    [Fact]
    public void Identifies_the_game_mode_of_a_stratagem_from_its_type_label()
    {
        var result = RulesTextMapper.MapCoreStratagems(CoreFile("core-stratagems.json"));

        Assert.Equal(GameMode.Challenger, Assert.Single(result.Stratagems, s => s.Name == "ALL IN").Mode);
        Assert.Equal(GameMode.BoardingActions, Assert.Single(result.Stratagems, s => s.Name == "BATTLEFIELD COMMAND").Mode);
        Assert.Equal(GameMode.Standard, Assert.Single(result.Stratagems, s => s.Name == "CRUSHING IMPACT").Mode);
    }

    [Fact]
    public void Parses_cp_cost_and_classifies_a_known_stratagem_category()
    {
        var result = RulesTextMapper.MapCoreStratagems(CoreFile("core-stratagems.json"));

        var allIn = Assert.Single(result.Stratagems, s => s.Name == "ALL IN");
        Assert.Equal(0, allIn.CpCost);
        Assert.Equal(StratagemCategory.StrategicPloy, allIn.Category);
    }

    [Fact]
    public void Maps_detachment_dp_mission_tag_and_enhancement_count()
    {
        var result = RulesTextMapper.MapFaction(FactionFile("orks"));

        var blitz = Assert.Single(result.Detachments, d => d.Name == "Blitz Brigade");
        Assert.Equal(1, blitz.DetachmentPoints);
        Assert.Equal(MissionArchetype.TakeAndHold, blitz.Archetype);
        Assert.Equal(2, blitz.EnhancementCount);
        Assert.Equal(GameMode.Standard, blitz.Mode);
    }

    [Fact]
    public void Boarding_actions_detachments_are_marked_as_such()
    {
        var result = RulesTextMapper.MapFaction(FactionFile("orks"));

        Assert.Contains(result.Detachments, d => d.Mode == GameMode.BoardingActions);
        Assert.All(result.Detachments.Where(d => d.Mode == GameMode.Standard), d => Assert.NotNull(d.Archetype));
    }

    [Fact]
    public void Splits_a_stratagem_description_into_markdown_fields()
    {
        var result = RulesTextMapper.MapFaction(FactionFile("orks"));

        var krunch = Assert.Single(result.Stratagems, s => s.Name == "IMPENDING KRUNCH");
        Assert.Equal("Your Charge phase, when a friendly Wagon unit ends a charge move.", krunch.When);
        Assert.Equal("That **WAGON** unit.", krunch.Target);
        Assert.Contains("**battle-shock roll**", krunch.Effect);
        Assert.Equal("Blitz Brigade", krunch.Detachment);
    }

    [Fact]
    public void No_mapped_stratagem_keeps_html_markup()
    {
        var core = RulesTextMapper.MapCoreStratagems(CoreFile("core-stratagems.json"));
        var faction = RulesTextMapper.MapFaction(FactionFile("space-marines"));

        var text = core.Stratagems.Concat(faction.Stratagems)
            .Select(s => string.Join('\n', s.When, s.Target, s.Effect, s.Restrictions, s.AdditionalText));

        Assert.All(text, t => Assert.DoesNotMatch("<[a-zA-Z/]", t));
    }

    [Fact]
    public void Maps_a_real_faction_with_stratagems_abilities_and_enhancements()
    {
        var result = RulesTextMapper.MapFaction(FactionFile("astra-militarum"));

        Assert.NotEmpty(result.Stratagems);
        Assert.NotEmpty(result.Abilities);
        Assert.NotEmpty(result.Enhancements);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Reads_the_faction_identity_and_its_datasheet_list()
    {
        var result = RulesTextMapper.MapFaction(FactionFile("orks"));

        Assert.Equal("orks", result.Identity.Slug);
        Assert.Equal("Orks", result.Identity.Name);
        Assert.NotEmpty(result.Identity.DataVersion);
        Assert.Contains(result.Datasheets, d => d.Name == "Warboss" && !d.IsLegends);
        Assert.Contains(result.Datasheets, d => d.IsLegends);
    }

    [Fact]
    public void Reads_the_upgrade_flag_on_an_upgrade_tagged_enhancement()
    {
        var result = RulesTextMapper.MapFaction(FactionFile("astra-militarum"));

        Assert.Contains(result.Enhancements, e => e.IsUpgrade);
    }

    [Fact]
    public void Invalid_json_is_a_handled_warning_not_an_exception()
    {
        var result = RulesTextMapper.MapFaction("not json at all");

        Assert.Empty(result.Stratagems);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Reads_the_stratagem_ids_out_of_a_datasheet_file()
    {
        var json = File.ReadAllText(TestPaths.WwwrootDataFile("factions", "orks", "datasheets", "000000001.json"));

        var links = RulesTextMapper.MapDatasheetDetails(json);

        Assert.NotNull(links);
        Assert.Equal("000000001", links.Id);
        Assert.NotEmpty(links.StratagemIds);
    }

    [Fact]
    public void An_unreadable_datasheet_file_yields_null_instead_of_throwing()
    {
        Assert.Null(RulesTextMapper.MapDatasheetDetails("<html>not json</html>"));
    }

    [Fact]
    public void Maps_core_rules_chapters_to_markdown()
    {
        var result = RulesTextMapper.MapCoreRules(CoreFile("moving.json"));

        var moving = Assert.Single(result.Rules, r => r.Id == "03.moving");
        Assert.Equal(CoreRuleSource.RulesText, moving.Source);
        Assert.DoesNotContain("<p>", moving.Markdown);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Invalid_core_rules_json_is_a_handled_warning_not_an_exception()
    {
        var result = RulesTextMapper.MapCoreRules("not json at all");

        Assert.Empty(result.Rules);
        Assert.NotEmpty(result.Warnings);
    }
}
