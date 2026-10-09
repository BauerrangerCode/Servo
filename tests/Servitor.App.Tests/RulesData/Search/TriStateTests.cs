using Servitor.App.RulesData.Search;

namespace Servitor.App.Tests.RulesData.Search;

public class TriStateTests
{
    [Theory]
    [InlineData(TriState.Neutral, TriState.Include)]
    [InlineData(TriState.Include, TriState.Exclude)]
    [InlineData(TriState.Exclude, TriState.Neutral)]
    public void Cycle_advances_neutral_include_exclude_and_back(TriState start, TriState expected)
    {
        Assert.Equal(expected, start.Cycle());
    }
}
