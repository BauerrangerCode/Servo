using Servitor.RulesData.Mapping;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.RulesData.Merging;

public class AllyResolverTests
{
    private const string Marines = "Imperium - Space Marines.json";
    private const string Agents = "Imperium - Agents of the Imperium.json";
    private const string Knights = "Imperium - Imperial Knights - Library.json";
    private const string Legends = "Library - Astartes Heresy Legends.json";
    private const string Unaligned = "Unaligned Forces.json";
    private const string OrksFile = "Orks.json";

    private static CatalogueLinks Catalogue(string file, params string[] linkedIds) => new(file, file, linkedIds);

    [Fact]
    public void An_army_may_take_allies_from_the_factions_its_catalogue_links_to()
    {
        var allies = AllyResolver.Resolve([Catalogue(Marines, Agents, Knights), Catalogue(Agents), Catalogue(Knights)]);

        Assert.Equal(["imperial-agents", "imperial-knights"], allies["space-marines"]);
    }

    [Fact]
    public void Links_to_the_own_library_to_multi_faction_libraries_and_to_unmapped_catalogues_are_no_allies()
    {
        var allies = AllyResolver.Resolve(
        [
            Catalogue(Marines, Marines, Legends, Unaligned, "unknown-id"),
            Catalogue(Legends),
            Catalogue(Unaligned),
        ]);

        Assert.False(allies.ContainsKey("space-marines"));
    }

    [Fact]
    public void An_army_without_links_has_no_allies_and_an_unmapped_catalogue_gives_none()
    {
        var allies = AllyResolver.Resolve([Catalogue(OrksFile, Unaligned), Catalogue(Unaligned), Catalogue("Something Unmapped.json", Agents), Catalogue(Agents)]);

        Assert.Empty(allies);
    }

    [Fact]
    public void A_catalogue_shared_by_several_chapters_adds_their_allies_to_the_one_faction_without_duplicates()
    {
        var allies = AllyResolver.Resolve(
        [
            Catalogue(Marines, Agents),
            Catalogue("Imperium - Blood Angels.json", Agents, Knights),
            Catalogue(Agents),
            Catalogue(Knights),
        ]);

        Assert.Equal(["imperial-agents", "imperial-knights"], allies["space-marines"]);
    }

    [Fact]
    public void The_real_catalogues_let_space_marines_ally_with_imperial_agents_and_knights_but_not_orks()
    {
        var links = FactionCatalogueMap.MappedCatalogueFileNames
            .Select(file =>
            {
                var result = BsDataCatalogueMapper.Map(File.ReadAllText(TestPaths.WwwrootDataFile("bsdata", "wh40k-11e", file)));
                return new CatalogueLinks(file, result.CatalogueId, result.LinkedCatalogueIds);
            })
            .ToList();

        var allies = AllyResolver.Resolve(links);

        Assert.Contains("imperial-agents", allies["space-marines"]);
        Assert.Contains("imperial-knights", allies["space-marines"]);
        Assert.DoesNotContain("space-marines", allies["space-marines"]);
        Assert.Contains("astra-militarum", allies["genestealer-cults"]);
        Assert.False(allies.ContainsKey("orks"));
    }
}
