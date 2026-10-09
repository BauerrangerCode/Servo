namespace Servitor.RulesData.Merging;

/// <summary>
/// Every raw source file the data merge reads, addressed relative to the data root (<c>wwwroot/data/</c>). A fixed
/// list rather than a directory scan because the browser cannot enumerate a static folder; a test keeps it in sync
/// with the files actually shipped.
/// </summary>
public static class RulesSourceManifest
{
    /// <summary>
    /// The BSData source repository's commit at the time of copying; part of the merged data version.
    /// </summary>
    public const string BsDataVersion = "18afd24";

    public const string BsDataDirectory = "bsdata/wh40k-11e";

    public const string CoreDirectory = "core";

    public const string FactionsDirectory = "factions";

    public const string FactionFileName = "faction.json";

    public const string CoreStratagemsFileName = "core-stratagems.json";

    /// <summary>
    /// The game-system file: its top-level JSON key is "gameSystem", not "catalogue", so it carries core rules
    /// rather than datasheets and is read separately from <see cref="BsDataCatalogueFileNames"/>.
    /// </summary>
    public const string BsDataGameSystemFileName = "Warhammer 40,000.json";

    public static readonly IReadOnlyList<string> BsDataCatalogueFileNames =
    [
        "Aeldari - Aeldari Library.json",
        "Aeldari - Craftworlds.json",
        "Aeldari - Drukhari.json",
        "Chaos - Chaos Daemons Library.json",
        "Chaos - Chaos Daemons.json",
        "Chaos - Chaos Knights Library.json",
        "Chaos - Chaos Knights.json",
        "Chaos - Chaos Space Marines.json",
        "Chaos - Death Guard.json",
        "Chaos - Emperor's Children.json",
        "Chaos - Thousand Sons.json",
        "Chaos - Titanicus Traitoris.json",
        "Chaos - World Eaters.json",
        "Genestealer Cults.json",
        "Imperium - Adepta Sororitas.json",
        "Imperium - Adeptus Custodes.json",
        "Imperium - Adeptus Mechanicus.json",
        "Imperium - Adeptus Titanicus.json",
        "Imperium - Agents of the Imperium.json",
        "Imperium - Astra Militarum - Library.json",
        "Imperium - Astra Militarum.json",
        "Imperium - Black Templars.json",
        "Imperium - Blood Angels.json",
        "Imperium - Dark Angels.json",
        "Imperium - Deathwatch.json",
        "Imperium - Grey Knights.json",
        "Imperium - Imperial Fists.json",
        "Imperium - Imperial Knights - Library.json",
        "Imperium - Imperial Knights.json",
        "Imperium - Iron Hands.json",
        "Imperium - Raven Guard.json",
        "Imperium - Salamanders.json",
        "Imperium - Space Marines.json",
        "Imperium - Space Wolves.json",
        "Imperium - Ultramarines.json",
        "Imperium - White Scars.json",
        "Leagues of Votann.json",
        "Library - Astartes Heresy Legends.json",
        "Library - Titans.json",
        "Library - Tyranids.json",
        "Necrons.json",
        "Orks.json",
        "T'au Empire.json",
        "Tyranids.json",
        "Unaligned Forces.json",
    ];

    /// <summary>
    /// The core rules prose files. <see cref="CoreStratagemsFileName"/> is deliberately not listed: it holds
    /// stratagems, not rules text, and is mapped separately.
    /// </summary>
    public static readonly IReadOnlyList<string> CoreRuleFileNames =
    [
        "actions.json",
        "aircraft.json",
        "attached-units.json",
        "attack-sequence.json",
        "charge-phase.json",
        "command-phase.json",
        "core-abilities.json",
        "core-concepts.json",
        "datasheets.json",
        "fight-phase.json",
        "flying-and-surging.json",
        "introduction.json",
        "making-attacks.json",
        "monsters-and-vehicles.json",
        "movement-phase.json",
        "moving.json",
        "muster-armies.json",
        "objectives.json",
        "other-concepts.json",
        "other-rules-and-abilities.json",
        "shooting-phase.json",
        "stratagems.json",
        "strategic-reserves.json",
        "terrain.json",
        "the-battle-round.json",
        "transports.json",
    ];

    public static readonly IReadOnlyList<string> FactionSlugs =
    [
        "adepta-sororitas",
        "adeptus-custodes",
        "adeptus-mechanicus",
        "adeptus-titanicus",
        "aeldari",
        "astra-militarum",
        "chaos-daemons",
        "chaos-knights",
        "chaos-space-marines",
        "death-guard",
        "drukhari",
        "emperor-s-children",
        "genestealer-cults",
        "grey-knights",
        "imperial-agents",
        "imperial-knights",
        "leagues-of-votann",
        "necrons",
        "orks",
        "space-marines",
        "t-au-empire",
        "thousand-sons",
        "tyranids",
        "world-eaters",
    ];
}
