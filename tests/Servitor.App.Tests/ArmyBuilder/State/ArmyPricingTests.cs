using Servitor.App.ArmyBuilder.State;
using Servitor.App.Tests.ArmyBuilder.Catalog;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.ArmyBuilder.State;

public class ArmyPricingTests
{
    // 1st and 2nd copy cost 90 (10 models) / 180 (20 models); the 3rd and further cost 100 / 200.
    private static readonly Datasheet Escalating = Units.Sheet(
        "Boyz",
        sizes: [new UnitSize(10, 90), new UnitSize(20, 180)],
        brackets:
        [
            new CostBracket(1, 2, [new UnitSize(10, 90), new UnitSize(20, 180)]),
            new CostBracket(3, null, [new UnitSize(10, 100), new UnitSize(20, 200)]),
        ]);

    private static ArmyUnit Unit(Datasheet datasheet, UnitSize size) => new(Guid.NewGuid(), datasheet, size);

    [Theory]
    [InlineData(1, 10, 90)]
    [InlineData(2, 10, 90)]
    [InlineData(3, 10, 100)]
    [InlineData(7, 20, 200)]
    [InlineData(2, 20, 180)]
    public void The_price_depends_on_the_copy_and_the_model_count(int copy, int models, int expected)
    {
        Assert.Equal(expected, ArmyPricing.PointsFor(Escalating, models, copy));
    }

    [Fact]
    public void A_datasheet_without_brackets_uses_its_size_list_and_then_its_own_points()
    {
        var plain = Units.Sheet("Warboss", points: 80, sizes: [new UnitSize(1, 75)]);

        Assert.Equal(75, ArmyPricing.PointsFor(plain, 1, 4));
        Assert.Equal(80, ArmyPricing.PointsFor(Units.Sheet("Odd", points: 80), 0, 1));
    }

    [Fact]
    public void Each_unit_is_the_next_copy_of_its_datasheet_in_list_order()
    {
        var other = Units.Sheet("Warboss", points: 80, sizes: [new UnitSize(1, 80)]);
        var units = new[]
        {
            Unit(Escalating, new UnitSize(10, 90)),
            Unit(other, new UnitSize(1, 80)),
            Unit(Escalating, new UnitSize(10, 90)),
            Unit(Escalating, new UnitSize(10, 90)),
        };

        var priced = ArmyPricing.Price(units);

        Assert.Equal([1, 1, 2, 3], priced.Select(p => p.Copy));
        Assert.Equal([90, 80, 90, 100], priced.Select(p => p.Points));
    }

    [Fact]
    public void Removing_an_earlier_copy_lowers_the_price_of_the_later_ones()
    {
        var units = Enumerable.Range(0, 3).Select(_ => Unit(Escalating, new UnitSize(10, 90))).ToList();
        Assert.Equal(280, ArmyPricing.Price(units).Sum(p => p.Points));

        units.RemoveAt(0);

        Assert.Equal(180, ArmyPricing.Price(units).Sum(p => p.Points));
    }

    [Fact]
    public void A_unit_without_a_price_for_its_model_count_keeps_the_points_it_was_added_with()
    {
        var unit = Unit(Units.Sheet("Odd", points: 55), new UnitSize(0, 55));

        Assert.Equal(55, Assert.Single(ArmyPricing.Price([unit])).Points);
    }

    [Fact]
    public void Totals_count_points_units_models_and_wounds()
    {
        var boyz = Units.Sheet("Boyz", wounds: "1", sizes: [new UnitSize(10, 90)]);
        var warboss = Units.Sheet("Warboss", wounds: "5", sizes: [new UnitSize(1, 80)]);
        var units = new[] { Unit(boyz, new UnitSize(10, 90)), Unit(warboss, new UnitSize(1, 80)) };

        var totals = ArmyTotals.Of(ArmyPricing.Price(units));

        Assert.Equal(new ArmyTotals(Points: 170, Units: 2, Models: 11, Wounds: 15), totals);
    }

    [Fact]
    public void An_unknown_model_count_counts_as_one_model_and_a_non_numeric_wound_value_as_zero()
    {
        var odd = Units.Sheet("Odd", points: 55, wounds: "3+D3");

        var totals = ArmyTotals.Of(ArmyPricing.Price([Unit(odd, new UnitSize(0, 55))]));

        Assert.Equal(new ArmyTotals(55, 1, 1, 0), totals);
    }

    [Fact]
    public void An_empty_army_has_empty_totals()
    {
        Assert.Equal(ArmyTotals.Empty, ArmyTotals.Of([]));
    }
}
