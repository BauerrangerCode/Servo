using Servitor.App.ArmyBuilder.Catalog;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.ArmyBuilder.Catalog;

internal static class Units
{
    public static Datasheet Sheet(
        string name,
        string[]? keywords = null,
        string[]? factions = null,
        int points = 100,
        UnitSize[]? sizes = null,
        CompositionConstraint? constraint = null,
        string catalogue = "Xenos - Orks",
        CostBracket[]? brackets = null,
        string wounds = "1") =>
        new(
            name,
            catalogue,
            points,
            new StatLine("6\"", "3", "5+", null, wounds, "7+", "2"),
            [],
            [],
            [],
            [],
            [],
            keywords ?? ["Infantry"],
            constraint,
            [])
        {
            FactionSlugs = factions ?? ["orks"],
            Sizes = sizes ?? [],
            CostBrackets = brackets ?? [],
        };
}

public class UnitCatalogTests
{
    [Theory]
    [InlineData(new[] { "Character", "Epic Hero" }, UnitRole.EpicHero)]
    [InlineData(new[] { "Infantry", "Character" }, UnitRole.Character)]
    [InlineData(new[] { "Infantry", "Battleline" }, UnitRole.Battleline)]
    [InlineData(new[] { "Vehicle", "Dedicated Transport" }, UnitRole.Transport)]
    [InlineData(new[] { "Vehicle", "Transport" }, UnitRole.Other)]
    [InlineData(new[] { "Fortification" }, UnitRole.Fortification)]
    [InlineData(new[] { "Infantry" }, UnitRole.Other)]
    public void The_role_is_the_most_specific_one_the_keywords_give(string[] keywords, UnitRole expected)
    {
        Assert.Equal(expected, UnitRoles.Of(Units.Sheet("X", keywords)));
    }

    [Fact]
    public void A_faction_catalog_holds_the_units_of_that_army_including_shared_ones_ordered_by_name()
    {
        var all = new[]
        {
            Units.Sheet("Warboss"),
            Units.Sheet("Cadian Shock Troops", factions: ["astra-militarum", "genestealer-cults"]),
            Units.Sheet("Boyz"),
            Units.Sheet("Stray", factions: []),
        };

        Assert.Equal(["Boyz", "Warboss"], UnitCatalog.ForFaction(all, "orks", showLegends: false).Select(d => d.Name));
        Assert.Equal(["Cadian Shock Troops"], UnitCatalog.ForFaction(all, "genestealer-cults", showLegends: false).Select(d => d.Name));
    }

    [Fact]
    public void Current_and_legends_units_are_kept_apart()
    {
        var all = new[] { Units.Sheet("Warboss"), Units.Sheet("Old Warboss [Legends]") };

        Assert.Equal(["Warboss"], UnitCatalog.ForFaction(all, "orks", showLegends: false).Select(d => d.Name));
        Assert.Equal(["Old Warboss [Legends]"], UnitCatalog.ForFaction(all, "orks", showLegends: true).Select(d => d.Name));
    }

    [Fact]
    public void Units_are_grouped_by_role_in_catalog_order_and_empty_groups_are_left_out()
    {
        var units = new[]
        {
            Units.Sheet("Trukk", ["Vehicle", "Dedicated Transport"]),
            Units.Sheet("Boyz", ["Infantry", "Battleline"]),
            Units.Sheet("Ghazghkull", ["Character", "Epic Hero"]),
            Units.Sheet("Painboy", ["Character"]),
            Units.Sheet("Gretchin"),
        };

        var groups = UnitCatalog.Group(units, "", role: null);

        Assert.Equal(
            [UnitRole.EpicHero, UnitRole.Character, UnitRole.Battleline, UnitRole.Transport, UnitRole.Other],
            groups.Select(g => g.Role));
        Assert.Equal(["Trukk"], groups[3].Units.Select(u => u.Name));
    }

    [Fact]
    public void The_query_narrows_by_name_ignoring_case_and_the_role_narrows_to_one_group()
    {
        var units = new[]
        {
            Units.Sheet("Boyz", ["Battleline"]),
            Units.Sheet("Nob Boyz"),
            Units.Sheet("Painboy", ["Character"]),
        };

        Assert.Equal(["Boyz", "Nob Boyz"], UnitCatalog.Group(units, " boyz ", null).SelectMany(g => g.Units).Select(u => u.Name).Order());
        Assert.Equal([UnitRole.Battleline], UnitCatalog.Group(units, "boyz", UnitRole.Battleline).Select(g => g.Role));
        Assert.Empty(UnitCatalog.Group(units, "nothing", null));
    }

    [Fact]
    public void Sizes_show_as_a_range_and_units_without_a_size_list_fall_back_to_their_own_points()
    {
        var squad = Units.Sheet("Boyz", sizes: [new UnitSize(10, 90), new UnitSize(20, 180)]);
        var hero = Units.Sheet("Warboss", sizes: [new UnitSize(1, 80)]);
        var unknown = Units.Sheet("Odd", points: 55);

        Assert.Equal("10-20", UnitSizes.ModelsText(squad));
        Assert.Equal("90-180", UnitSizes.PointsText(squad));
        Assert.Equal("1", UnitSizes.ModelsText(hero));
        Assert.Equal("", UnitSizes.ModelsText(unknown));
        Assert.Equal("55", UnitSizes.PointsText(unknown));
        Assert.Equal([new UnitSize(0, 55)], UnitSizes.For(unknown));
    }

    [Fact]
    public void The_per_army_limit_uses_the_incursion_override_only_in_incursion()
    {
        var unit = Units.Sheet("Boyz", constraint: new CompositionConstraint(6, 4));

        Assert.Equal(4, UnitLimits.MaxPerArmy(unit, BattleSizes.Incursion));
        Assert.Equal(6, UnitLimits.MaxPerArmy(unit, BattleSizes.StrikeForce));
        Assert.Equal(6, UnitLimits.MaxPerArmy(unit, BattleSizes.Onslaught));
    }

    [Fact]
    public void Without_an_incursion_override_the_base_limit_applies_everywhere_and_without_a_constraint_there_is_no_limit()
    {
        var epic = Units.Sheet("Ghazghkull", constraint: new CompositionConstraint(1, null));

        Assert.Equal(1, UnitLimits.MaxPerArmy(epic, BattleSizes.Incursion));
        Assert.Null(UnitLimits.MaxPerArmy(Units.Sheet("Free"), BattleSizes.Incursion));
    }
}

public class UnitChaptersTests
{
    [Theory]
    [InlineData("Imperium - Adeptus Astartes - Blood Angels", "Blood Angels")]
    [InlineData("Imperium - Adeptus Astartes - Space Marines", null)]
    [InlineData("Xenos - Orks", null)]
    [InlineData("Imperium - Adeptus Custodes", null)]
    public void The_chapter_comes_from_the_catalogue_and_the_general_catalogue_has_none(string catalogue, string? expected)
    {
        Assert.Equal(expected, UnitChapters.Of(Units.Sheet("X", catalogue: catalogue)));
    }

    [Fact]
    public void Chapter_names_are_compared_ignoring_case_and_punctuation()
    {
        Assert.True(UnitChapters.Same("SPACE WOLVES", "Space Wolves"));
        Assert.False(UnitChapters.Same("Dark Angels", "Blood Angels"));
    }
}
