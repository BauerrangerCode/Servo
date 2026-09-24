namespace Servitor.RulesData.Merging;

/// <summary>
/// Which rules-text factions a BSData catalogue's datasheets are matched against. BSData and the rules-text source
/// group units differently: BSData has one catalogue per chapter or shared "Library" catalogue, the rules-text
/// source one faction per army. The mapping was derived from the datasheet-name overlap between the two sources;
/// a catalogue absent here (e.g. "Unaligned Forces") has no counterpart and is reported as unmapped.
/// </summary>
public static class FactionCatalogueMap
{
    private static readonly Dictionary<string, IReadOnlyList<string>> FactionSlugsByCatalogue =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Aeldari - Aeldari Library.json"] = ["aeldari", "drukhari"],
            ["Chaos - Chaos Daemons Library.json"] = ["chaos-daemons"],
            ["Chaos - Chaos Knights Library.json"] = ["chaos-knights"],
            ["Chaos - Chaos Space Marines.json"] = ["chaos-space-marines"],
            ["Chaos - Death Guard.json"] = ["death-guard"],
            ["Chaos - Emperor's Children.json"] = ["emperor-s-children"],
            ["Chaos - Thousand Sons.json"] = ["thousand-sons"],
            ["Chaos - World Eaters.json"] = ["world-eaters"],
            ["Genestealer Cults.json"] = ["genestealer-cults"],
            ["Imperium - Adepta Sororitas.json"] = ["adepta-sororitas"],
            ["Imperium - Adeptus Custodes.json"] = ["adeptus-custodes"],
            ["Imperium - Adeptus Mechanicus.json"] = ["adeptus-mechanicus"],
            ["Imperium - Agents of the Imperium.json"] = ["imperial-agents"],
            ["Imperium - Astra Militarum - Library.json"] = ["astra-militarum"],
            ["Imperium - Black Templars.json"] = ["space-marines"],
            ["Imperium - Blood Angels.json"] = ["space-marines"],
            ["Imperium - Dark Angels.json"] = ["space-marines"],
            ["Imperium - Deathwatch.json"] = ["space-marines", "imperial-agents"],
            ["Imperium - Grey Knights.json"] = ["grey-knights"],
            ["Imperium - Imperial Fists.json"] = ["space-marines"],
            ["Imperium - Imperial Knights - Library.json"] = ["imperial-knights"],
            ["Imperium - Iron Hands.json"] = ["space-marines"],
            ["Imperium - Raven Guard.json"] = ["space-marines"],
            ["Imperium - Salamanders.json"] = ["space-marines"],
            ["Imperium - Space Marines.json"] = ["space-marines"],
            ["Imperium - Space Wolves.json"] = ["space-marines"],
            ["Imperium - Ultramarines.json"] = ["space-marines"],
            ["Imperium - White Scars.json"] = ["space-marines"],
            ["Leagues of Votann.json"] = ["leagues-of-votann"],
            ["Library - Astartes Heresy Legends.json"] = ["chaos-space-marines", "space-marines"],
            ["Library - Titans.json"] = ["adeptus-titanicus"],
            ["Library - Tyranids.json"] = ["tyranids"],
            ["Necrons.json"] = ["necrons"],
            ["Orks.json"] = ["orks"],
            ["T'au Empire.json"] = ["t-au-empire"],
            ["Tyranids.json"] = ["tyranids"],
        };

    public static IReadOnlyList<string> FactionSlugsFor(string catalogueFileName) =>
        FactionSlugsByCatalogue.GetValueOrDefault(catalogueFileName) ?? [];

    public static IEnumerable<string> MappedCatalogueFileNames => FactionSlugsByCatalogue.Keys;
}
