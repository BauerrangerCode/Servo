namespace Servitor.RulesData.Domain;

/// <summary>
/// <paramref name="DetachmentPoints"/> is the detachment's DP cost against the Battle Size's budget.
/// <paramref name="Archetype"/> is null when the source gives the detachment no mission affinity.
/// </summary>
public sealed record Detachment(
    string Id,
    string Name,
    string Legend,
    int DetachmentPoints,
    MissionArchetype? Archetype,
    int EnhancementCount,
    GameMode Mode)
{
    /// <summary>
    /// The one Space Marines Chapter this detachment restricts the army to ("Your army can include Iron Hands units, but
    /// it cannot include any ADEPTUS ASTARTES units drawn from any other Chapter"), or null when it has no such
    /// restriction. Read from the detachment's rules text, the only place the source states it.
    /// </summary>
    public string? RestrictedToChapter { get; init; }
}
