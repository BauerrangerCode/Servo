namespace Servitor.RulesData.Domain;

/// <summary>The display label for each <see cref="MissionArchetype"/> — shared by Build's detachment tags and Scoring's disposition picker.</summary>
public static class MissionArchetypeLabels
{
    public static string Label(MissionArchetype archetype) => archetype switch
    {
        MissionArchetype.TakeAndHold => "Take and Hold",
        MissionArchetype.PriorityAssets => "Priority Assets",
        MissionArchetype.PurgeTheFoe => "Purge the Foe",
        MissionArchetype.Disruption => "Disruption",
        MissionArchetype.Reconnaissance => "Reconnaissance",
        _ => archetype.ToString(),
    };
}
