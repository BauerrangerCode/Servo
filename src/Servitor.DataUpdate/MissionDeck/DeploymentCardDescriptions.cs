namespace Servitor.DataUpdate.MissionDeck;

/// <summary>One deployment card: where each side sets up and where the objectives lie, in words.</summary>
public sealed record DeploymentCardDescription(string AttackerZone, string DefenderZone, string Objectives);

/// <summary>
/// The Mission Deck page shows a deployment card only as a picture, so the text the Scoring screen displays cannot
/// be scraped. These descriptions were written from the card pictures and are matched to the scraped card by name; a
/// card that is not listed here keeps its name and gets empty descriptions (the scraper reports it).
/// </summary>
public static class DeploymentCardDescriptions
{
    private static readonly Dictionary<string, DeploymentCardDescription> Cards = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Hammer and Anvil"] = new(
            "A deep zone along the top (short) battlefield edge.",
            "A deep zone along the bottom (short) battlefield edge, facing the Attacker's across a wide central No Man's Land.",
            "One central objective at the exact centre; two further objectives set left/right of centre within No Man's Land; one home objective (offset to one side) inside each player's own zone."),
        ["Dawn of War"] = new(
            "A shallow zone along the left (long) battlefield edge.",
            "A shallow zone along the right (long) battlefield edge, facing the Attacker's across a wide central No Man's Land.",
            "One central objective at the exact centre; two further objectives offset above/below centre; one home objective centred on each player's own edge."),
        ["Search and Destroy"] = new(
            "The top-left quarter of the battlefield.",
            "The bottom-right quarter of the battlefield — diagonally opposite the Attacker's.",
            "One central objective at the exact centre (contested within a 9\" radius); one home objective in each player's own quarter; one further objective in each of the two neutral quarters, near the diagonal."),
        ["Crucible of Battle"] = new(
            "The triangular half of the battlefield above a single corner-to-corner diagonal.",
            "The triangular half below that same diagonal.",
            "One central objective on the diagonal at the exact centre; two further objectives further along the diagonal on each side of centre; one home objective in each player's own corner."),
        ["Tipping Point"] = new(
            "A staggered zone along the top edge that steps diagonally toward the centre, deepest at the corners and shallowest in the middle.",
            "The mirrored staggered zone along the bottom edge.",
            "One central objective on the staircase boundary at the exact centre; one home objective in each player's own corner; further objectives placed along the boundary near the middle of the battlefield."),
        ["Sweeping Engagement"] = new(
            "A staggered zone along the left edge that steps diagonally toward the centre.",
            "The mirrored staggered zone along the right edge.",
            "One central objective on the staircase boundary at the exact centre; one home objective in each player's own corner; further objectives placed along the boundary near the middle of the battlefield."),
    };

    public static DeploymentCardDescription? Find(string name) => Cards.GetValueOrDefault(name);
}
