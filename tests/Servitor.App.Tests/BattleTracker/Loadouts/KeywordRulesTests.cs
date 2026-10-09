using Servitor.App.BattleTracker.Units;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.BattleTracker.Loadouts;

public class KeywordRulesTests
{
    private static CoreRule Rule(string name, CoreRuleKind kind = CoreRuleKind.WeaponRule, string? number = null) =>
        new(name, name, number, $"Text of {name}.", CoreRuleSource.BsData, kind);

    private static readonly KeywordRules Rules = new(
    [
        Rule("Lethal Hits"), Rule("Rapid Fire"), Rule("Anti"), Rule("Close-quarters"),
        Rule("Feel No Pain 4+", CoreRuleKind.UnitRule), Rule("Infantry", CoreRuleKind.General), Rule("Flying Models", CoreRuleKind.General),
    ]);

    [Theory]
    [InlineData("Lethal Hits", "Lethal Hits")]
    [InlineData("LETHAL HITS", "Lethal Hits")]
    [InlineData("[LETHAL HITS]", "Lethal Hits")]
    [InlineData("LETHAL HITS: non-MONSTER/VEHICLE", "Lethal Hits")]
    [InlineData("Rapid Fire 2", "Rapid Fire")]
    [InlineData("Anti-Infantry 4+", "Anti")]
    [InlineData("Close-Quarters", "Close-quarters")]
    [InlineData("Fly", "Flying Models")]
    [InlineData("Feel No Pain 4+", "Feel No Pain 4+")]
    [InlineData("Infantry", "Infantry")]
    public void A_keyword_finds_the_rule_of_its_name_with_or_without_value_or_note(string keyword, string expectedRule)
    {
        Assert.Equal(expectedRule, Rules.Find(keyword)?.Name);
    }

    [Theory]
    [InlineData("Imperium")]
    [InlineData("Faction: Orks")]
    [InlineData("-")]
    [InlineData("")]
    public void A_keyword_without_a_core_rule_has_none(string keyword)
    {
        Assert.Null(Rules.Find(keyword));
    }

    [Fact]
    public void A_weapon_or_unit_rule_wins_over_a_general_entry_of_the_same_name()
    {
        var rules = new KeywordRules([Rule("Blast", CoreRuleKind.General), Rule("Blast")]);

        Assert.Equal("Text of Blast.", rules.Find("Blast")!.Markdown);
    }

    [Fact]
    public void A_chapter_keyword_shows_the_introduction_and_every_numbered_section_of_the_chapter_in_order()
    {
        var rules = new KeywordRules(
        [
            Rule("Monsters and Vehicles", CoreRuleKind.General),
            Rule("Shooting at Engaged Monsters and Vehicles", CoreRuleKind.General, "17.03"),
            Rule("Moving Monsters and Vehicles", CoreRuleKind.General, "17.01"),
            Rule("Transport Capacity", CoreRuleKind.General, "18.01"),
        ]);

        var rule = rules.Find("Vehicle")!;

        Assert.Equal("Monsters and Vehicles", rule.Name);
        var intro = rule.Markdown.IndexOf("Text of Monsters and Vehicles.", StringComparison.Ordinal);
        var moving = rule.Markdown.IndexOf("### 17.01 Moving Monsters and Vehicles", StringComparison.Ordinal);
        var shooting = rule.Markdown.IndexOf("### 17.03 Shooting at Engaged Monsters and Vehicles", StringComparison.Ordinal);
        Assert.True(intro >= 0 && intro < moving && moving < shooting);
        Assert.DoesNotContain("Transport Capacity", rule.Markdown);
    }

    [Fact]
    public void A_chapter_named_directly_stays_its_own_entry_without_the_sections()
    {
        var rules = new KeywordRules([Rule("Aircraft", CoreRuleKind.General), Rule("Other", CoreRuleKind.General, "17.01")]);

        Assert.Equal("Text of Aircraft.", rules.Find("Aircraft")!.Markdown);
    }
}
