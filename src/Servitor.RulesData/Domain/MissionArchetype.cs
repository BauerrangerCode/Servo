namespace Servitor.RulesData.Domain;

/// <summary>
/// The five mission archetypes. One shared type: a detachment's mission-affinity tag (Build) and a player's
/// disposition (Scoring) are the same concept and must not drift apart.
/// </summary>
public enum MissionArchetype
{
    TakeAndHold,
    Disruption,
    PriorityAssets,
    PurgeTheFoe,
    Reconnaissance,
}
