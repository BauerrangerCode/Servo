using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.RulesData.Merging;

public class NameNormalizerTests
{
    [Theory]
    [InlineData("Warboss In Mega Armour", "Warboss in Mega Armour")]
    [InlineData("Nob’s Boyz", "Nob's Boyz")]
    [InlineData("Warboss on Warbike [Legends]", "Warboss on Warbike")]
    [InlineData("Twin-linked  Guns", "twin linked guns")]
    [InlineData("[TORRENT]", "Torrent")]
    public void Names_that_differ_only_in_formatting_share_one_key(string first, string second)
    {
        Assert.Equal(NameNormalizer.Normalize(first), NameNormalizer.Normalize(second));
    }

    [Fact]
    public void Different_names_keep_different_keys()
    {
        Assert.NotEqual(NameNormalizer.Normalize("Plague Sorcerer"), NameNormalizer.Normalize("Plague Sorcerer [Crucible]"));
    }

    [Theory]
    [InlineData("Warboss on Warbike [Legends]", true)]
    [InlineData("warboss [legends]", true)]
    [InlineData("Warboss", false)]
    public void Recognises_the_legends_suffix(string name, bool expected)
    {
        Assert.Equal(expected, NameNormalizer.IsLegends(name));
    }
}
