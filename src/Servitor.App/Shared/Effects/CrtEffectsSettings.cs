namespace Servitor.App.Shared.Effects;

/// <summary>Random screen artifacts appear once every <see cref="MinSeconds"/> to <see cref="MaxSeconds"/>.</summary>
public sealed record CrtEffectsSettings(bool Enabled, double MinSeconds, double MaxSeconds)
{
    public const double LowestSeconds = 0.5;

    public static CrtEffectsSettings Default { get; } = new(true, 0.5, 1.5);

    /// <summary>Clamps the interval to something usable: at least <see cref="LowestSeconds"/>, and max never below min.</summary>
    public CrtEffectsSettings Normalized()
    {
        var min = double.IsFinite(MinSeconds) ? Math.Max(LowestSeconds, MinSeconds) : Default.MinSeconds;
        var max = double.IsFinite(MaxSeconds) ? Math.Max(min, MaxSeconds) : Math.Max(min, Default.MaxSeconds);
        return this with { MinSeconds = min, MaxSeconds = max };
    }
}
