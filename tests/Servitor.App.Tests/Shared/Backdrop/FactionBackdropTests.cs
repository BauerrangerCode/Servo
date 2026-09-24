using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.Shared.Backdrop;

namespace Servitor.App.Tests.Shared.Backdrop;

public class FactionBackdropTests : BunitContext
{
    private const string UnalignedForces = "Unaligned Forces";

    [Fact]
    public void Every_faction_name_in_the_merged_data_resolves_to_an_existing_faction_folder()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(TestPaths.WwwrootDataFile("merged", "rules-data.json")));
        var factions = document.RootElement.GetProperty("Datasheets").EnumerateArray()
            .Select(d => d.GetProperty("Faction").GetString()!)
            .Distinct()
            .Where(f => f != UnalignedForces);

        Assert.All(factions, faction =>
        {
            var slug = FactionBackdrop.SlugForCatalogue(faction);
            Assert.NotNull(slug);
            Assert.True(Directory.Exists(TestPaths.WwwrootDataFile("factions", slug)), $"{faction} -> {slug}");
        });
    }

    [Fact]
    public void Showing_a_faction_raises_changed_once_and_ignores_repeats()
    {
        var backdrop = new FactionBackdrop();
        var changes = 0;
        backdrop.Changed += () => changes++;

        backdrop.ShowFaction("orks");
        backdrop.ShowFaction("orks");

        Assert.Equal("orks", backdrop.FactionSlug);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Showing_a_catalogue_uses_its_faction_slug_and_ignores_unknown_names()
    {
        var backdrop = new FactionBackdrop();

        backdrop.ShowCatalogue("Xenos - Orks");
        Assert.Equal("orks", backdrop.FactionSlug);

        backdrop.ShowCatalogue(UnalignedForces);
        Assert.Equal("orks", backdrop.FactionSlug);
    }

    [Fact]
    public void The_view_renders_nothing_until_a_faction_was_clicked_and_then_the_faction_image()
    {
        var backdrop = new FactionBackdrop();
        Services.AddSingleton<IFactionBackdrop>(backdrop);
        var cut = Render<FactionBackdropView>();

        Assert.Empty(cut.FindAll("img"));

        backdrop.ShowFaction("necrons");

        var image = cut.Find("img.faction-backdrop");
        Assert.Equal("data/assets/factions/necrons.png", image.GetAttribute("src"));
        Assert.Equal("data/assets/factions/generic.png", image.GetAttribute("data-generic"));
    }
}
