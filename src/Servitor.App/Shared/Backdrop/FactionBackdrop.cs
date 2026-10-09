namespace Servitor.App.Shared.Backdrop;

public sealed class FactionBackdrop : IFactionBackdrop
{
    // A datasheet's faction is the catalogue name stored inside the BSData file, which follows neither the file name
    // nor the rules-text faction slug, so the two are joined here explicitly. Chapters share the Space Marines icon.
    private static readonly Dictionary<string, string> SlugsByCatalogueName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Aeldari - Aeldari Library"] = "aeldari",
        ["Chaos - Chaos Knights Library"] = "chaos-knights",
        ["Chaos - Chaos Space Marines"] = "chaos-space-marines",
        ["Chaos - Daemons Library"] = "chaos-daemons",
        ["Chaos - Death Guard"] = "death-guard",
        ["Chaos - Emperor's Children"] = "emperor-s-children",
        ["Chaos - Thousand Sons"] = "thousand-sons",
        ["Chaos - World Eaters"] = "world-eaters",
        ["Imperium - Adepta Sororitas"] = "adepta-sororitas",
        ["Imperium - Adeptus Astartes - Black Templars"] = "space-marines",
        ["Imperium - Adeptus Astartes - Blood Angels"] = "space-marines",
        ["Imperium - Adeptus Astartes - Dark Angels"] = "space-marines",
        ["Imperium - Adeptus Astartes - Deathwatch"] = "space-marines",
        ["Imperium - Adeptus Astartes - Imperial Fists"] = "space-marines",
        ["Imperium - Adeptus Astartes - Iron Hands"] = "space-marines",
        ["Imperium - Adeptus Astartes - Raven Guard"] = "space-marines",
        ["Imperium - Adeptus Astartes - Salamanders"] = "space-marines",
        ["Imperium - Adeptus Astartes - Space Marines"] = "space-marines",
        ["Imperium - Adeptus Astartes - Space Wolves"] = "space-marines",
        ["Imperium - Adeptus Astartes - Ultramarines"] = "space-marines",
        ["Imperium - Adeptus Astartes - White Scars"] = "space-marines",
        ["Imperium - Adeptus Custodes"] = "adeptus-custodes",
        ["Imperium - Adeptus Mechanicus"] = "adeptus-mechanicus",
        ["Imperium - Agents of the Imperium"] = "imperial-agents",
        ["Imperium - Astra Militarum"] = "astra-militarum",
        ["Imperium - Grey Knights"] = "grey-knights",
        ["Imperium - Imperial Knights"] = "imperial-knights",
        ["Library - Astartes Heresy Legends"] = "space-marines",
        ["Library - Titans"] = "adeptus-titanicus",
        ["Library - Tyranids"] = "tyranids",
        ["Xenos - Genestealer Cults"] = "genestealer-cults",
        ["Xenos - Leagues of Votann"] = "leagues-of-votann",
        ["Xenos - Necrons"] = "necrons",
        ["Xenos - Orks"] = "orks",
        ["Xenos - T'au Empire"] = "t-au-empire",
        ["Xenos - Tyranids"] = "tyranids",
    };

    public event Action? Changed;

    public string? FactionSlug { get; private set; }

    public static string? SlugForCatalogue(string catalogueName) =>
        SlugsByCatalogueName.GetValueOrDefault(catalogueName);

    public void ShowFaction(string factionSlug)
    {
        if (factionSlug == FactionSlug)
        {
            return;
        }

        FactionSlug = factionSlug;
        Changed?.Invoke();
    }

    public void ShowCatalogue(string catalogueName)
    {
        if (SlugForCatalogue(catalogueName) is { } slug)
        {
            ShowFaction(slug);
        }
    }
}
