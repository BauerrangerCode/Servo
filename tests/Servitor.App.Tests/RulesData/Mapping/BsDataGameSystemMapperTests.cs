using Servitor.RulesData.Domain;
using Servitor.RulesData.Mapping;

namespace Servitor.App.Tests.RulesData.Mapping;

public class BsDataGameSystemMapperTests
{
    [Fact]
    public void Maps_the_shared_rules_of_the_real_game_system_file_untouched()
    {
        var json = File.ReadAllText(TestPaths.WwwrootDataFile("bsdata", "wh40k-11e", "Warhammer 40,000.json"));

        var result = BsDataGameSystemMapper.Map(json);

        var deepStrike = Assert.Single(result.Rules, r => r.Name == "Deep Strike");
        Assert.Equal(CoreRuleSource.BsData, deepStrike.Source);
        Assert.Contains("ingress move", deepStrike.Markdown);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Tells_weapon_rules_from_unit_rules_on_the_real_game_system_file()
    {
        var json = File.ReadAllText(TestPaths.WwwrootDataFile("bsdata", "wh40k-11e", "Warhammer 40,000.json"));

        var rules = BsDataGameSystemMapper.Map(json).Rules;

        var weaponRules = new[] { "Blast", "Hazardous", "Devastating Wounds", "Rapid Fire", "Twin-linked", "Close-quarters", "Anti" };
        var unitRules = new[] { "Deep Strike", "Leader", "Feel No Pain", "Deadly Demise", "Scouts", "Fights First" };
        Assert.All(weaponRules, name => Assert.Equal(CoreRuleKind.WeaponRule, Assert.Single(rules, r => r.Name == name).Kind));
        Assert.All(unitRules, name => Assert.Equal(CoreRuleKind.UnitRule, Assert.Single(rules, r => r.Name == name).Kind));
        Assert.DoesNotContain(rules, r => r.Kind is CoreRuleKind.General or CoreRuleKind.Action);
    }

    [Fact]
    public void A_document_without_a_game_system_root_is_a_handled_warning()
    {
        var result = BsDataGameSystemMapper.Map("{\"catalogue\": {}}");

        Assert.Empty(result.Rules);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Invalid_json_is_a_handled_warning_not_an_exception()
    {
        var result = BsDataGameSystemMapper.Map("not json");

        Assert.Empty(result.Rules);
        Assert.NotEmpty(result.Warnings);
    }
}
