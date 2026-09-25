using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.RulesData.Domain;

public class WargearLimitsTests
{
    private static LimitAdjustment When(ModelCountOperator op, int threshold, int value, bool sets = true) =>
        new(sets, value, [new ModelCountCondition(op, threshold)]);

    [Theory]
    [InlineData(ModelCountOperator.AtLeast, 10, 9, 3)]
    [InlineData(ModelCountOperator.AtLeast, 10, 10, 8)]
    [InlineData(ModelCountOperator.GreaterThan, 10, 10, 3)]
    [InlineData(ModelCountOperator.GreaterThan, 10, 11, 8)]
    [InlineData(ModelCountOperator.AtMost, 10, 10, 8)]
    [InlineData(ModelCountOperator.AtMost, 10, 11, 3)]
    [InlineData(ModelCountOperator.LessThan, 10, 9, 8)]
    [InlineData(ModelCountOperator.LessThan, 10, 10, 3)]
    [InlineData(ModelCountOperator.EqualTo, 10, 10, 8)]
    [InlineData(ModelCountOperator.EqualTo, 10, 11, 3)]
    public void An_adjustment_applies_only_while_its_condition_on_the_model_count_holds(ModelCountOperator op, int threshold, int models, int expected)
    {
        var limit = new WargearLimit(3, [When(op, threshold, 8)]);

        Assert.Equal(expected, WargearLimits.At(limit, models));
    }

    [Fact]
    public void Increments_add_and_sets_replace_in_order()
    {
        var limit = new WargearLimit(1, [When(ModelCountOperator.AtLeast, 10, 2, sets: false), When(ModelCountOperator.AtLeast, 20, 6)]);

        Assert.Equal(1, WargearLimits.At(limit, 5));
        Assert.Equal(3, WargearLimits.At(limit, 10));
        Assert.Equal(6, WargearLimits.At(limit, 20));
    }

    [Fact]
    public void All_conditions_of_an_adjustment_must_hold_and_a_limit_never_drops_below_zero()
    {
        var both = new LimitAdjustment(true, 5, [new ModelCountCondition(ModelCountOperator.AtLeast, 10), new ModelCountCondition(ModelCountOperator.AtMost, 15)]);
        var limit = new WargearLimit(1, [both]);

        Assert.Equal(1, WargearLimits.At(limit, 9));
        Assert.Equal(5, WargearLimits.At(limit, 12));
        Assert.Equal(1, WargearLimits.At(limit, 16));
        Assert.Equal(0, WargearLimits.At(new WargearLimit(1, [When(ModelCountOperator.AtLeast, 1, -4, sets: false)]), 5));
    }

    [Fact]
    public void A_pools_cap_is_its_smallest_limit_and_a_pool_without_limits_cannot_be_taken()
    {
        var pool = new WargearPool("Big Shoota", null, [new WargearLimit(2, []), new WargearLimit(1, []), new WargearLimit(3, [])]);

        Assert.Equal(1, WargearLimits.MaxFor(pool, 10));
        Assert.Equal(0, WargearLimits.MaxFor(new WargearPool("X", null, []), 10));
    }
}
