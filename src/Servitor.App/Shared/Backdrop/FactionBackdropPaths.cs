namespace Servitor.App.Shared.Backdrop;

public static class FactionBackdropPaths
{
    /// <summary>Where a faction's icon lives; <c>wwwroot/data</c> is gitignored, so the images are never committed.</summary>
    public static string ImagePath(string factionSlug) => $"data/assets/factions/{factionSlug}.png";

    /// <summary>Shown when a faction has no image of its own.</summary>
    public static string GenericImagePath { get; } = ImagePath("generic");
}
