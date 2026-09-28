using Servitor.App.ArmyBuilder.State;
using Servitor.App.ArmyBuilder.Validation;
using Servitor.App.Tests.ArmyBuilder.Catalog;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.ArmyBuilder.Validation;

internal static class Army
{
    public static ArmySetup Setup(BattleSize? size = null, params Detachment[] detachments) =>
        new(size ?? BattleSizes.StrikeForce, "orks", detachments);

    public static Detachment Detachment(string name, int dp = 1, string? chapter = null) =>
        new($"id-{name}", name, "", dp, MissionArchetype.TakeAndHold, 4, GameMode.Standard) { RestrictedToChapter = chapter };

    public static Enhancement Enhancement(string name, string detachment = "Blitz", int cost = 10, bool upgrade = false) =>
        new($"e-{name}", name, "", "", cost, detachment, upgrade, null);

    public static ArmyUnit Unit(Datasheet datasheet, int models = 1, int points = 100, Enhancement? enhancement = null) =>
        new(Guid.NewGuid(), datasheet, new UnitSize(models, points)) { Enhancement = enhancement };

    public static Datasheet Hero(string name = "Warboss") => Units.Sheet(name, ["Character", "Infantry"], points: 100);

    public static Datasheet Epic(string name = "Ghazghkull") => Units.Sheet(name, ["Character", "Epic Hero"], points: 200);

    public static Datasheet Squad(string name = "Boyz") => Units.Sheet(name, ["Infantry", "Battleline"], points: 90);

    public static IReadOnlyList<ArmyIssue> Check(IArmyRule rule, ArmySetup setup, params ArmyUnit[] units) =>
        rule.Check(ArmyContext.Create(setup, units)).ToList();
}

public class ArmyValidatorTests
{
    [Fact]
    public void An_army_that_follows_every_rule_has_no_issues()
    {
        var unit = Army.Unit(Army.Hero(), points: 100, enhancement: Army.Enhancement("Kit"));
        var setup = Army.Setup(BattleSizes.StrikeForce, Army.Detachment("Blitz"));

        Assert.Empty(ArmyValidator.Validate(setup, [unit]));
    }

    [Fact]
    public void Every_broken_rule_is_reported_on_its_own()
    {
        var setup = Army.Setup(BattleSizes.Incursion, Army.Detachment("Blitz", dp: 2), Army.Detachment("Bully", dp: 2));
        var boss = Army.Epic();
        var units = new[]
        {
            Army.Unit(boss, points: 600),
            Army.Unit(boss, points: 600, enhancement: Army.Enhancement("Kit")),
        };

        var kinds = ArmyValidator.Validate(setup, units).Select(i => i.Kind).ToList();

        Assert.Contains(ArmyIssueKind.OverPointsBudget, kinds);
        Assert.Contains(ArmyIssueKind.OverDetachmentPoints, kinds);
        Assert.Contains(ArmyIssueKind.EnhancementNotAllowed, kinds);
        Assert.Equal(kinds.Count, kinds.Distinct().Count());
    }

    [Fact]
    public void An_empty_army_without_a_battle_size_has_no_issues()
    {
        Assert.Empty(ArmyValidator.Validate(ArmySetup.Empty, []));
    }
}

public class PointsBudgetRuleTests
{
    [Theory]
    [InlineData(1000, false)]
    [InlineData(1001, true)]
    public void Exceeding_the_points_total_of_the_battle_size_is_reported_with_amount_and_limit(int points, bool broken)
    {
        var issues = Army.Check(new PointsBudgetRule(), Army.Setup(BattleSizes.Incursion), Army.Unit(Army.Hero(), points: points));

        Assert.Equal(broken, issues.Count == 1);
        if (broken)
        {
            Assert.Equal(ArmyIssueKind.OverPointsBudget, issues[0].Kind);
            Assert.Equal("The army costs 1001 pts, 1 over the 1000 pts limit of Incursion.", issues[0].Message);
        }
    }

    [Fact]
    public void The_limit_is_the_one_of_the_battle_size_and_a_missing_battle_size_has_none()
    {
        var unit = Army.Unit(Army.Hero(), points: 1500);

        Assert.Empty(Army.Check(new PointsBudgetRule(), Army.Setup(BattleSizes.StrikeForce), unit));
        Assert.Empty(Army.Check(new PointsBudgetRule(), ArmySetup.Empty, unit));
    }
}

public class DetachmentPointsRuleTests
{
    [Fact]
    public void Detachments_costing_more_dp_than_the_battle_size_gives_are_reported()
    {
        var setup = Army.Setup(BattleSizes.StrikeForce, Army.Detachment("A", 2), Army.Detachment("B", 2));

        var issue = Assert.Single(Army.Check(new DetachmentPointsRule(), setup));

        Assert.Equal(ArmyIssueKind.OverDetachmentPoints, issue.Kind);
        Assert.Equal("The detachments cost 4 DP, over the 3 DP of Strike Force.", issue.Message);
    }

    [Fact]
    public void Detachments_within_the_budget_are_fine()
    {
        Assert.Empty(Army.Check(new DetachmentPointsRule(), Army.Setup(BattleSizes.StrikeForce, Army.Detachment("A", 2), Army.Detachment("B", 1))));
    }

    [Fact]
    public void An_incursion_army_may_take_a_single_three_dp_detachment_but_not_two_detachments_adding_up_to_it()
    {
        Assert.Empty(Army.Check(new DetachmentPointsRule(), Army.Setup(BattleSizes.Incursion, Army.Detachment("Big", 3))));
        Assert.Single(Army.Check(new DetachmentPointsRule(), Army.Setup(BattleSizes.Incursion, Army.Detachment("A", 2), Army.Detachment("B", 1))));
        Assert.Single(Army.Check(new DetachmentPointsRule(), Army.Setup(BattleSizes.StrikeForce, Army.Detachment("Big", 4))));
    }
}

public class UnitLimitRuleTests
{
    private static Datasheet Limited(int strikeForce, int? incursion) =>
        Units.Sheet("Boyz", constraint: new CompositionConstraint(strikeForce, incursion));

    [Fact]
    public void More_copies_than_the_datasheets_limit_are_reported_by_name_with_count_and_limit()
    {
        var boyz = Limited(6, 4);
        var units = Enumerable.Range(0, 5).Select(_ => Army.Unit(boyz)).ToArray();

        var issue = Assert.Single(Army.Check(new UnitLimitRule(), Army.Setup(BattleSizes.Incursion), units));

        Assert.Equal(ArmyIssueKind.OverUnitLimit, issue.Kind);
        Assert.Equal("Boyz: 5 units, but at most 4 are allowed in Incursion.", issue.Message);
    }

    [Fact]
    public void The_same_five_copies_are_fine_in_a_bigger_battle_size()
    {
        var units = Enumerable.Range(0, 5).Select(_ => Army.Unit(Limited(6, 4))).ToArray();

        Assert.Empty(Army.Check(new UnitLimitRule(), Army.Setup(BattleSizes.StrikeForce), units));
    }

    [Fact]
    public void An_epic_hero_may_be_taken_once_whatever_the_battle_size()
    {
        var epic = Units.Sheet("Ghazghkull", ["Character", "Epic Hero"], constraint: new CompositionConstraint(1, null));

        Assert.Single(Army.Check(new UnitLimitRule(), Army.Setup(BattleSizes.Onslaught), Army.Unit(epic), Army.Unit(epic)));
        Assert.Empty(Army.Check(new UnitLimitRule(), Army.Setup(BattleSizes.Onslaught), Army.Unit(epic)));
    }

    [Fact]
    public void Datasheets_without_a_limit_are_never_reported()
    {
        var free = Units.Sheet("Free");

        Assert.Empty(Army.Check(new UnitLimitRule(), Army.Setup(), Enumerable.Range(0, 20).Select(_ => Army.Unit(free)).ToArray()));
    }
}

public class ModelCountRuleTests
{
    private static readonly Datasheet Boyz = Units.Sheet("Boyz", sizes: [new UnitSize(10, 90), new UnitSize(20, 180)]);

    [Fact]
    public void A_model_count_the_datasheet_does_not_offer_is_reported_with_the_allowed_sizes()
    {
        var issue = Assert.Single(Army.Check(new ModelCountRule(), Army.Setup(), Army.Unit(Boyz, models: 15)));

        Assert.Equal(ArmyIssueKind.InvalidModelCount, issue.Kind);
        Assert.Equal("Boyz has 15 models, but it can only be taken with 10 or 20.", issue.Message);
    }

    [Fact]
    public void Offered_sizes_and_units_without_size_data_are_fine()
    {
        Assert.Empty(Army.Check(new ModelCountRule(), Army.Setup(), Army.Unit(Boyz, models: 20), Army.Unit(Units.Sheet("Odd"), models: 0)));
    }
}

public class ChapterRestrictionRuleTests
{
    private static Datasheet FromChapter(string name, string chapterCatalogue) =>
        Units.Sheet(name, catalogue: $"Imperium - Adeptus Astartes - {chapterCatalogue}");

    private static readonly ArmySetup IronHandsOnly = Army.Setup(BattleSizes.StrikeForce, Army.Detachment("Hammer of Avernii", chapter: "Iron Hands"));

    [Fact]
    public void A_unit_of_another_chapter_in_a_detachment_restricted_to_one_chapter_is_reported()
    {
        var issue = Assert.Single(Army.Check(new ChapterRestrictionRule(), IronHandsOnly, Army.Unit(FromChapter("Blood Angel Captain", "Blood Angels"))));

        Assert.Equal(ArmyIssueKind.ChapterMix, issue.Kind);
        Assert.Equal("Hammer of Avernii restricts the army to Iron Hands units, but it includes Blood Angel Captain (Blood Angels).", issue.Message);
    }

    [Fact]
    public void Units_of_the_chapter_itself_and_general_space_marines_units_are_fine()
    {
        Assert.Empty(Army.Check(
            new ChapterRestrictionRule(),
            IronHandsOnly,
            Army.Unit(FromChapter("Iron Priest", "Iron Hands")),
            Army.Unit(FromChapter("Intercessors", "Space Marines")),
            Army.Unit(Units.Sheet("Boyz"))));
    }

    [Fact]
    public void The_chapter_name_is_compared_ignoring_case()
    {
        var shouting = Army.Setup(BattleSizes.StrikeForce, Army.Detachment("Champions of Fenris", chapter: "SPACE WOLVES"));

        Assert.Empty(Army.Check(new ChapterRestrictionRule(), shouting, Army.Unit(FromChapter("Wolf Guard", "Space Wolves"))));
    }

    [Fact]
    public void Without_a_restricted_detachment_chapters_may_be_mixed_and_each_unit_is_named_once()
    {
        var mixed = new[] { Army.Unit(FromChapter("A", "Blood Angels")), Army.Unit(FromChapter("B", "Dark Angels")) };
        Assert.Empty(Army.Check(new ChapterRestrictionRule(), Army.Setup(BattleSizes.StrikeForce, Army.Detachment("Gladius")), mixed));

        var twice = Army.Check(new ChapterRestrictionRule(), IronHandsOnly, Army.Unit(FromChapter("A", "Blood Angels")), Army.Unit(FromChapter("A", "Blood Angels")));
        Assert.Single(twice);
    }
}

public class EnhancementRulesTests
{
    private static ArmySetup Blitz(BattleSize? size = null) => Army.Setup(size ?? BattleSizes.StrikeForce, Army.Detachment("Blitz"));

    [Fact]
    public void An_ordinary_enhancement_on_a_non_character_unit_is_reported()
    {
        var issue = Assert.Single(Army.Check(new EnhancementEligibilityRule(), Blitz(), Army.Unit(Army.Squad(), enhancement: Army.Enhancement("Kit"))));

        Assert.Equal(ArmyIssueKind.EnhancementNotAllowed, issue.Kind);
        Assert.Contains("only Character units can take an enhancement that is not an Upgrade", issue.Message);
    }

    [Fact]
    public void An_epic_hero_cannot_take_any_enhancement_not_even_an_upgrade()
    {
        var issues = Army.Check(
            new EnhancementEligibilityRule(),
            Blitz(),
            Army.Unit(Army.Epic(), enhancement: Army.Enhancement("Kit")),
            Army.Unit(Army.Epic(), enhancement: Army.Enhancement("Boost", upgrade: true)));

        Assert.Equal(2, issues.Count);
        Assert.All(issues, i => Assert.Contains("Epic Heroes cannot take enhancements", i.Message));
    }

    [Fact]
    public void Characters_can_take_ordinary_enhancements_and_non_characters_can_take_upgrades()
    {
        Assert.Empty(Army.Check(
            new EnhancementEligibilityRule(),
            Blitz(),
            Army.Unit(Army.Hero(), enhancement: Army.Enhancement("Kit")),
            Army.Unit(Army.Squad(), enhancement: Army.Enhancement("Boost", upgrade: true))));
    }

    [Fact]
    public void The_same_ordinary_enhancement_on_two_units_is_reported()
    {
        var kit = Army.Enhancement("Kit");

        var issue = Assert.Single(Army.Check(new EnhancementUniquenessRule(), Blitz(), Army.Unit(Army.Hero(), enhancement: kit), Army.Unit(Army.Hero("Painboy"), enhancement: kit)));

        Assert.Equal(ArmyIssueKind.DuplicateEnhancement, issue.Kind);
        Assert.Equal("Kit is taken 2 times; an army can include an enhancement only once.", issue.Message);
    }

    [Fact]
    public void Three_copies_of_an_upgrade_are_valid_and_a_fourth_is_reported()
    {
        var boost = Army.Enhancement("Boost", upgrade: true);
        var three = Enumerable.Range(0, 3).Select(_ => Army.Unit(Army.Squad(), enhancement: boost)).ToArray();
        Assert.Empty(Army.Check(new EnhancementUniquenessRule(), Blitz(), three));

        var issue = Assert.Single(Army.Check(new EnhancementUniquenessRule(), Blitz(), [.. three, Army.Unit(Army.Squad(), enhancement: boost)]));

        Assert.Equal(ArmyIssueKind.TooManyUpgradeCopies, issue.Kind);
        Assert.Equal("The Upgrade Boost is taken 4 times; an army can include at most 3.", issue.Message);
    }

    [Fact]
    public void More_enhancements_than_the_battle_size_allows_are_reported()
    {
        var units = Enumerable.Range(0, 3).Select(i => Army.Unit(Army.Hero($"Hero {i}"), enhancement: Army.Enhancement($"Kit {i}"))).ToArray();

        var issue = Assert.Single(Army.Check(new EnhancementLimitRule(), Blitz(BattleSizes.Incursion), units));

        Assert.Equal(ArmyIssueKind.TooManyEnhancements, issue.Kind);
        Assert.Equal("The army has 3 enhancements, over the limit of 2 of Incursion.", issue.Message);
        Assert.Empty(Army.Check(new EnhancementLimitRule(), Blitz(BattleSizes.StrikeForce), units));
    }

    [Fact]
    public void Only_the_first_copy_of_an_upgrade_counts_against_the_limit()
    {
        var boost = Army.Enhancement("Boost", upgrade: true);
        var units = new[]
        {
            Army.Unit(Army.Hero(), enhancement: Army.Enhancement("Kit")),
            Army.Unit(Army.Squad(), enhancement: boost),
            Army.Unit(Army.Squad(), enhancement: boost),
            Army.Unit(Army.Squad(), enhancement: boost),
        };

        Assert.Empty(Army.Check(new EnhancementLimitRule(), Blitz(BattleSizes.Incursion), units));
    }

    [Fact]
    public void An_enhancement_of_a_detachment_the_army_does_not_include_is_reported()
    {
        var issue = Assert.Single(Army.Check(new EnhancementDetachmentRule(), Blitz(), Army.Unit(Army.Hero(), enhancement: Army.Enhancement("Kit", detachment: "Bully Boyz"))));

        Assert.Equal(ArmyIssueKind.EnhancementDetachmentNotSelected, issue.Kind);
        Assert.Equal("Kit on Warboss belongs to the detachment Bully Boyz, which the army does not include.", issue.Message);
        Assert.Empty(Army.Check(new EnhancementDetachmentRule(), Blitz(), Army.Unit(Army.Hero(), enhancement: Army.Enhancement("Kit", detachment: "blitz"))));
    }

    [Fact]
    public void The_enhancements_cost_is_part_of_the_units_points()
    {
        var unit = Army.Unit(Army.Hero(), points: 100, enhancement: Army.Enhancement("Kit", cost: 25));

        var priced = Assert.Single(ArmyContext.Create(Blitz(), [unit]).Priced);

        Assert.Equal((125, 25), (priced.Points, priced.EnhancementPoints));
    }
}
