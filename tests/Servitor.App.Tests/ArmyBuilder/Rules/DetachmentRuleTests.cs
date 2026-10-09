using Servitor.App.ArmyBuilder.Catalog;
using Servitor.App.ArmyBuilder.Rules;
using Servitor.App.ArmyBuilder.State;
using Servitor.App.ArmyBuilder.Validation;
using Servitor.App.Tests.ArmyBuilder.Catalog;
using Servitor.App.Tests.ArmyBuilder.Validation;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.ArmyBuilder.Rules;

internal static class RuleData
{
    public static UnitSelector Selector(
        string[]? names = null,
        string[]? keywords = null,
        UnitRole[]? roles = null,
        string[]? allKeywords = null,
        string[]? excludeNames = null,
        string[]? excludeKeywords = null,
        bool? outside = null) =>
        new(names ?? [], keywords ?? [], allKeywords ?? [], roles ?? [], excludeNames ?? [], excludeKeywords ?? [], outside);

    public static DetachmentRuleSet Set(params DetachmentRule[] rules) => new(rules, []);

    public static ArmySetup SetupWith(string faction, string detachment, BattleSize? size = null) =>
        new(size ?? BattleSizes.StrikeForce, faction, [Army.Detachment(detachment)]);

    /// <summary>A unit of the datasheet as the given catalogue and armies list it.</summary>
    public static Datasheet Sheet(string name, string[]? keywords = null, string[]? factions = null, int points = 100) =>
        Units.Sheet(name, keywords, factions ?? ["chaos-knights"], points);

    public static IReadOnlyList<ArmyIssue> Check(IArmyRule rule, ArmySetup setup, DetachmentRuleSet rules, params ArmyUnit[] units) =>
        rule.Check(ArmyContext.Create(setup, units, rules)).ToList();
}

public class DetachmentRuleParserTests
{
    private static string Rule(string type, string extra = "", string units = "\"units\":{\"keywords\":[\"WAR DOG\"]}") =>
        $"{{\"faction\":\"chaos-knights\",\"detachment\":\"Houndpack Lance\",\"type\":\"{type}\",\"label\":\"WAR DOG units\",{units}{(extra.Length > 0 ? "," + extra : "")}}}";

    private static DetachmentRuleSet Parse(params string[] rules) => DetachmentRuleParser.Parse("{\"rules\":[" + string.Join(",", rules) + "]}");

    [Fact]
    public void Every_rule_type_is_read_with_its_own_parameters()
    {
        var set = Parse(
            Rule("mustInclude", "\"min\":3"),
            Rule("cannotInclude"),
            Rule("unitLimit", "\"max\":3"),
            Rule("pointsCap", "\"caps\":{\"Incursion\":500,\"Strike Force\":1000}"));

        Assert.Empty(set.Warnings);
        Assert.Equal(3, Assert.IsType<MustIncludeRule>(set.Rules[0]).Min);
        Assert.IsType<CannotIncludeRule>(set.Rules[1]);
        Assert.Equal(3, Assert.IsType<UnitLimitOverrideRule>(set.Rules[2]).Max);
        var cap = Assert.IsType<PointsCapRule>(set.Rules[3]);
        Assert.Equal(500, cap.CapsByBattleSize["incursion"]);
        Assert.Equal(1000, cap.CapsByBattleSize["Strike Force"]);
    }

    [Fact]
    public void The_common_fields_and_the_selector_are_read()
    {
        var rule = Assert.Single(Parse(Rule(
            "mustInclude",
            "\"min\":1",
            "\"units\":{\"names\":[\"Yvraine\"],\"keywords\":[\"WAR DOG\"],\"allKeywords\":[\"Z\"],\"roles\":[\"epicHero\"],\"excludeNames\":[\"X\"],\"excludeKeywords\":[\"Y\"],\"outsideFaction\":true}")).Rules);

        Assert.Equal(("chaos-knights", "Houndpack Lance", "WAR DOG units"), (rule.Faction, rule.Detachment, rule.Label));
        Assert.Equal(["Yvraine"], rule.Units.Names);
        Assert.Equal(["WAR DOG"], rule.Units.Keywords);
        Assert.Equal(["Z"], rule.Units.AllKeywords);
        Assert.Equal([UnitRole.EpicHero], rule.Units.Roles);
        Assert.Equal(["X"], rule.Units.ExcludeNames);
        Assert.Equal(["Y"], rule.Units.ExcludeKeywords);
        Assert.True(rule.Units.OutsideFaction);
    }

    [Fact]
    public void A_rule_without_units_is_about_every_unit()
    {
        var rule = Assert.Single(Parse("{\"faction\":\"a\",\"detachment\":\"b\",\"type\":\"cannotInclude\",\"label\":\"c\"}").Rules);

        Assert.Equal(UnitSelector.Any, rule.Units);
    }

    [Fact]
    public void Comments_trailing_commas_and_notes_are_allowed()
    {
        var json = "{ // rules\n\"rules\":[{\"faction\":\"a\",\"detachment\":\"b\",\"type\":\"cannotInclude\",\"label\":\"c\",\"note\":\"why\",},],}";

        Assert.Single(DetachmentRuleParser.Parse(json).Rules);
    }

    [Theory]
    [InlineData("{\"faction\":\"a\",\"detachment\":\"b\",\"type\":\"whatever\",\"label\":\"c\"}", "unknown type")]
    [InlineData("{\"detachment\":\"b\",\"type\":\"cannotInclude\",\"label\":\"c\"}", "no \"faction\"")]
    [InlineData("{\"faction\":\"a\",\"type\":\"cannotInclude\",\"label\":\"c\"}", "no \"detachment\"")]
    [InlineData("{\"faction\":\"a\",\"detachment\":\"b\",\"type\":\"cannotInclude\"}", "no \"label\"")]
    [InlineData("{\"faction\":\"a\",\"detachment\":\"b\",\"type\":\"mustInclude\",\"label\":\"c\"}", "\"min\"")]
    [InlineData("{\"faction\":\"a\",\"detachment\":\"b\",\"type\":\"mustInclude\",\"label\":\"c\",\"min\":0}", "\"min\"")]
    [InlineData("{\"faction\":\"a\",\"detachment\":\"b\",\"type\":\"unitLimit\",\"label\":\"c\"}", "\"max\"")]
    [InlineData("{\"faction\":\"a\",\"detachment\":\"b\",\"type\":\"pointsCap\",\"label\":\"c\"}", "\"caps\"")]
    [InlineData("{\"faction\":\"a\",\"detachment\":\"b\",\"type\":\"cannotInclude\",\"label\":\"c\",\"units\":{\"roles\":[\"Wizard\"]}}", "unknown role")]
    [InlineData("{\"faction\":\"a\",\"detachment\":\"b\",\"type\":\"cannotInclude\",\"label\":\"c\",\"units\":[]}", "\"units\"")]
    [InlineData("42", "not an object")]
    public void A_rule_that_cannot_be_read_is_left_out_and_named_in_the_warnings(string rule, string expected)
    {
        var set = Parse(rule, Rule("cannotInclude"));

        Assert.Single(set.Rules);
        var warning = Assert.Single(set.Warnings);
        Assert.StartsWith("Rule 1", warning, StringComparison.Ordinal);
        Assert.Contains(expected, warning, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"rules\":{}}")]
    public void A_broken_file_yields_no_rules_and_a_warning(string json)
    {
        var set = DetachmentRuleParser.Parse(json);

        Assert.Empty(set.Rules);
        Assert.Single(set.Warnings);
    }
}

public class UnitSelectorTests
{
    [Fact]
    public void Names_match_ignoring_case_punctuation_and_the_legends_marker()
    {
        var selector = RuleData.Selector(names: ["Be'lakor"]);

        Assert.True(selector.Matches(Units.Sheet("Be’lakor [Legends]"), "chaos-daemons"));
        Assert.True(selector.Matches(Units.Sheet("BE'LAKOR"), "chaos-daemons"));
        Assert.False(selector.Matches(Units.Sheet("Bloodthirster"), "chaos-daemons"));
    }

    [Fact]
    public void Keywords_and_roles_match_when_the_unit_has_any_of_them()
    {
        Assert.True(RuleData.Selector(keywords: ["war dog", "Titanic"]).Matches(Units.Sheet("X", ["Vehicle", "WAR DOG"]), null));
        Assert.False(RuleData.Selector(keywords: ["WAR DOG"]).Matches(Units.Sheet("X", ["Vehicle"]), null));
        Assert.True(RuleData.Selector(roles: [UnitRole.EpicHero, UnitRole.Transport]).Matches(Units.Sheet("X", ["Character", "Epic Hero"]), null));
        Assert.False(RuleData.Selector(roles: [UnitRole.EpicHero]).Matches(Units.Sheet("X", ["Character"]), null));
    }

    [Fact]
    public void Every_field_that_is_set_has_to_match_and_the_exclusions_win()
    {
        var epicNotBelakor = RuleData.Selector(roles: [UnitRole.EpicHero], excludeNames: ["Be'lakor"]);

        Assert.True(epicNotBelakor.Matches(Units.Sheet("Skarbrand", ["Epic Hero"]), null));
        Assert.False(epicNotBelakor.Matches(Units.Sheet("Be'lakor", ["Epic Hero"]), null));
        Assert.False(RuleData.Selector(keywords: ["Infantry"], excludeKeywords: ["Aircraft"]).Matches(Units.Sheet("X", ["Infantry", "Aircraft"]), null));
        Assert.False(RuleData.Selector(names: ["Boyz"], keywords: ["Vehicle"]).Matches(Units.Sheet("Boyz", ["Infantry"]), null));
    }

    [Fact]
    public void A_selector_can_pick_the_units_from_outside_or_inside_the_armys_faction()
    {
        var own = Units.Sheet("Own", factions: ["genestealer-cults"]);
        var borrowed = Units.Sheet("Borrowed", factions: ["astra-militarum"]);

        Assert.True(RuleData.Selector(outside: true).Matches(borrowed, "genestealer-cults"));
        Assert.False(RuleData.Selector(outside: true).Matches(own, "genestealer-cults"));
        Assert.True(RuleData.Selector(outside: false).Matches(own, "genestealer-cults"));
        Assert.True(RuleData.Selector().Matches(borrowed, "genestealer-cults"));
    }

    [Fact]
    public void All_keywords_are_needed_together_where_keywords_need_only_one()
    {
        var selector = RuleData.Selector(allKeywords: ["Faction: Tyranids", "Vanguard Invader"]);

        Assert.True(selector.Matches(Units.Sheet("Lictor", ["Faction: Tyranids", "Vanguard Invader", "Infantry"]), null));
        Assert.False(selector.Matches(Units.Sheet("Hive Tyrant", ["Faction: Tyranids", "Monster"]), null));
        Assert.False(selector.Matches(Units.Sheet("Acolytes", ["Vanguard Invader"]), null));
    }

    [Fact]
    public void A_selector_without_fields_matches_every_unit()
    {
        Assert.True(UnitSelector.Any.Matches(Units.Sheet("Anything", ["Whatever"]), "orks"));
    }
}

public class DetachmentRuleSetTests
{
    private static readonly MustIncludeRule Houndpack = new("chaos-knights", "Houndpack Lance", "WAR DOG units", UnitSelector.Any, 3);

    [Fact]
    public void A_rule_is_active_only_for_its_faction_and_while_the_army_has_the_detachment()
    {
        var rules = RuleData.Set(Houndpack);

        Assert.Single(rules.ActiveFor(RuleData.SetupWith("chaos-knights", "Houndpack Lance")));
        Assert.Empty(rules.ActiveFor(RuleData.SetupWith("orks", "Houndpack Lance")));
        Assert.Empty(rules.ActiveFor(RuleData.SetupWith("chaos-knights", "Infernal Lance")));
        Assert.Empty(rules.ActiveFor(ArmySetup.Empty));
    }

    [Fact]
    public void Detachment_names_are_compared_ignoring_case_and_punctuation()
    {
        var rules = RuleData.Set(Houndpack with { Detachment = "Reaper's Wager" });

        Assert.Single(rules.ActiveFor(RuleData.SetupWith("chaos-knights", "REAPER’S WAGER")));
    }
}

public class DetachmentRuleCheckTests
{
    private const string Faction = "chaos-knights";

    [Fact]
    public void Too_few_required_units_are_reported_with_the_count_the_army_has()
    {
        var rule = new MustIncludeRule(Faction, "Houndpack Lance", "WAR DOG units", RuleData.Selector(keywords: ["WAR DOG"]), 3);
        var dog = RuleData.Sheet("War Dog Stalker", ["Vehicle", "WAR DOG"]);

        var issue = Assert.Single(RuleData.Check(new MustIncludeCheck(), RuleData.SetupWith(Faction, "Houndpack Lance"), RuleData.Set(rule), Army.Unit(dog), Army.Unit(dog)));

        Assert.Equal(ArmyIssueKind.MissingRequiredUnit, issue.Kind);
        Assert.Equal("Houndpack Lance needs at least 3 WAR DOG units in the army, but it has 2.", issue.Message);
    }

    [Fact]
    public void Enough_required_units_satisfy_the_rule_and_so_does_not_having_the_detachment()
    {
        var rule = new MustIncludeRule(Faction, "Houndpack Lance", "WAR DOG units", RuleData.Selector(keywords: ["WAR DOG"]), 3);
        var dog = RuleData.Sheet("War Dog Stalker", ["WAR DOG"]);
        var three = new[] { Army.Unit(dog), Army.Unit(dog), Army.Unit(dog) };

        Assert.Empty(RuleData.Check(new MustIncludeCheck(), RuleData.SetupWith(Faction, "Houndpack Lance"), RuleData.Set(rule), three));
        Assert.Empty(RuleData.Check(new MustIncludeCheck(), RuleData.SetupWith(Faction, "Infernal Lance"), RuleData.Set(rule)));
    }

    [Fact]
    public void A_forbidden_unit_is_reported_by_name_once_per_datasheet()
    {
        var rule = new CannotIncludeRule("chaos-daemons", "Shadow Legion", "Daemon Prince units", RuleData.Selector(names: ["Daemon Prince"]));
        var prince = RuleData.Sheet("Daemon Prince", factions: ["chaos-daemons"]);

        var issue = Assert.Single(RuleData.Check(
            new CannotIncludeCheck(), RuleData.SetupWith("chaos-daemons", "Shadow Legion"), RuleData.Set(rule), Army.Unit(prince), Army.Unit(prince), Army.Unit(RuleData.Sheet("Bloodletters"))));

        Assert.Equal(ArmyIssueKind.ForbiddenUnit, issue.Kind);
        Assert.Equal("Shadow Legion does not allow Daemon Prince units in the army, but it includes Daemon Prince.", issue.Message);
    }

    [Fact]
    public void Units_the_rule_does_not_name_are_fine()
    {
        var rule = new CannotIncludeRule("chaos-daemons", "Shadow Legion", "Daemon Prince units", RuleData.Selector(names: ["Daemon Prince"]));

        Assert.Empty(RuleData.Check(new CannotIncludeCheck(), RuleData.SetupWith("chaos-daemons", "Shadow Legion"), RuleData.Set(rule), Army.Unit(RuleData.Sheet("Bloodletters"))));
    }

    private static PointsCapRule Cap() => new(
        "genestealer-cults",
        "Brood Brother Auxilia",
        "Astra Militarum units",
        RuleData.Selector(outside: true),
        new Dictionary<string, int> { ["Incursion"] = 500, ["Strike Force"] = 1000 });

    private static readonly Datasheet Borrowed = RuleData.Sheet("Cadian Shock Troops", factions: ["astra-militarum"]);
    private static readonly Datasheet Own = RuleData.Sheet("Acolyte Hybrids", factions: ["genestealer-cults"]);

    [Fact]
    public void Units_from_outside_that_cost_more_than_the_cap_are_reported_and_the_armys_own_units_do_not_count()
    {
        var setup = RuleData.SetupWith("genestealer-cults", "Brood Brother Auxilia", BattleSizes.Incursion);
        var units = new[] { Army.Unit(Borrowed, points: 300), Army.Unit(Borrowed, points: 300), Army.Unit(Own, points: 900) };

        var issue = Assert.Single(RuleData.Check(new PointsCapCheck(), setup, RuleData.Set(Cap()), units));

        Assert.Equal(ArmyIssueKind.OverDetachmentPointsCap, issue.Kind);
        Assert.Equal("Brood Brother Auxilia allows Astra Militarum units up to 500 pts in Incursion, but they cost 600 pts.", issue.Message);
    }

    [Fact]
    public void The_cap_follows_the_battle_size_and_a_battle_size_without_a_cap_has_none()
    {
        var units = new[] { Army.Unit(Borrowed, points: 300), Army.Unit(Borrowed, points: 300) };

        Assert.Empty(RuleData.Check(new PointsCapCheck(), RuleData.SetupWith("genestealer-cults", "Brood Brother Auxilia", BattleSizes.StrikeForce), RuleData.Set(Cap()), units));
        Assert.Empty(RuleData.Check(new PointsCapCheck(), RuleData.SetupWith("genestealer-cults", "Brood Brother Auxilia", BattleSizes.Onslaught), RuleData.Set(Cap()), units));
    }

    [Fact]
    public void A_unit_limit_rule_raises_the_limit_of_the_named_units_and_never_lowers_it()
    {
        var limit = new UnitLimitOverrideRule("aeldari", "Ghosts of the Webway", "Troupe Master", RuleData.Selector(names: ["Troupe Master"]), 3);
        var master = Units.Sheet("Troupe Master", constraint: new CompositionConstraint(2, 2));
        var strong = Units.Sheet("Troupe Master", constraint: new CompositionConstraint(6, 6));

        Assert.Equal(2, UnitLimits.MaxPerArmy(master, BattleSizes.Incursion, [], "aeldari"));
        Assert.Equal(3, UnitLimits.MaxPerArmy(master, BattleSizes.Incursion, [limit], "aeldari"));
        Assert.Equal(6, UnitLimits.MaxPerArmy(strong, BattleSizes.Incursion, [limit], "aeldari"));
        Assert.Null(UnitLimits.MaxPerArmy(Units.Sheet("Troupe Master"), BattleSizes.Incursion, [limit], "aeldari"));
        Assert.Equal(2, UnitLimits.MaxPerArmy(Units.Sheet("Other", constraint: new CompositionConstraint(2, 2)), BattleSizes.Incursion, [limit], "aeldari"));
    }

    [Fact]
    public void The_raised_limit_is_used_when_adding_units_and_when_checking_the_list()
    {
        var limit = new UnitLimitOverrideRule("orks", "Blitz", "Boyz", RuleData.Selector(names: ["Boyz"]), 3);
        var rules = RuleData.Set(limit);
        var boyz = Units.Sheet("Boyz", constraint: new CompositionConstraint(2, 2));
        var setup = RuleData.SetupWith("orks", "Blitz");
        var two = new[] { Army.Unit(boyz), Army.Unit(boyz) };

        Assert.Equal(AddUnitResult.AtLimit, UnitRules.CanAdd(setup, two, boyz));
        Assert.Equal(AddUnitResult.Added, UnitRules.CanAdd(setup, two, boyz, rules));

        var three = new[] { Army.Unit(boyz), Army.Unit(boyz), Army.Unit(boyz) };
        Assert.Single(ArmyValidator.Validate(setup, three));
        Assert.Empty(ArmyValidator.Validate(setup, three, rules));
    }
}
