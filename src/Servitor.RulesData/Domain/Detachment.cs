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
    GameMode Mode);
