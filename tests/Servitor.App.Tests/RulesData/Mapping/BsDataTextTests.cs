using Servitor.RulesData.Mapping;

namespace Servitor.App.Tests.RulesData.Mapping;

public class BsDataTextTests
{
    [Theory]
    [InlineData("**^^Infantry^^**", "**Infantry**")]
    [InlineData("^^**Vehicles^^**", "**Vehicles**")]
    [InlineData("Plain **bold** text", "Plain **bold** text")]
    public void Removes_keyword_markers_and_keeps_the_bold(string input, string expected) =>
        Assert.Equal(expected, BsDataText.CleanMarkdown(input));
}
