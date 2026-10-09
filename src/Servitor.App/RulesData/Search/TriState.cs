namespace Servitor.App.RulesData.Search;

/// <summary>A facet chip's tri-state: tap once to include, again to exclude, again to reset.</summary>
public enum TriState
{
    Neutral,
    Include,
    Exclude,
}

public static class TriStateExtensions
{
    public static TriState Cycle(this TriState state) => state switch
    {
        TriState.Neutral => TriState.Include,
        TriState.Include => TriState.Exclude,
        _ => TriState.Neutral,
    };
}
