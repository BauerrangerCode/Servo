namespace Servitor.App.Shared.Effects;

public interface ICrtEffectsService
{
    /// <summary>Loads the stored settings (or the defaults), starts the effects with them and returns them.</summary>
    Task<CrtEffectsSettings> LoadAsync();

    /// <summary>Persists the (normalized) settings and restarts the effects with them.</summary>
    Task<CrtEffectsSettings> ApplyAsync(CrtEffectsSettings settings);
}
