using Servitor.App.Shared.Effects;

namespace Servitor.App.Tests.Shared.Effects;

public class CrtEffectsSettingsTests
{
    [Fact]
    public void Default_is_half_a_second_to_one_and_a_half()
    {
        Assert.Equal(new CrtEffectsSettings(true, 0.5, 1.5), CrtEffectsSettings.Default);
    }

    [Fact]
    public void Max_below_min_is_raised_to_min()
    {
        var result = new CrtEffectsSettings(true, 4, 2).Normalized();

        Assert.Equal(4, result.MinSeconds);
        Assert.Equal(4, result.MaxSeconds);
    }

    [Fact]
    public void Too_small_values_are_raised_to_the_lowest_interval()
    {
        var result = new CrtEffectsSettings(true, 0, -5).Normalized();

        Assert.Equal(CrtEffectsSettings.LowestSeconds, result.MinSeconds);
        Assert.Equal(CrtEffectsSettings.LowestSeconds, result.MaxSeconds);
    }

    [Fact]
    public void Non_finite_values_fall_back_to_the_defaults()
    {
        var result = new CrtEffectsSettings(true, double.NaN, double.PositiveInfinity).Normalized();

        Assert.Equal(0.5, result.MinSeconds);
        Assert.True(double.IsFinite(result.MaxSeconds));
    }
}
