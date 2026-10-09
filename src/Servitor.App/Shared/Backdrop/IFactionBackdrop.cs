namespace Servitor.App.Shared.Backdrop;

/// <summary>
/// The faction whose icon is shown as the page background: the one the player clicked last, in any screen.
/// </summary>
public interface IFactionBackdrop
{
    event Action? Changed;

    /// <summary>Slug of the current faction (the folder name under <c>data/factions</c>), or null before any click.</summary>
    string? FactionSlug { get; }

    void ShowFaction(string factionSlug);

    /// <summary>For places that only know a datasheet's faction name (e.g. "Xenos - Orks"); unknown names are ignored.</summary>
    void ShowCatalogue(string catalogueName);
}
