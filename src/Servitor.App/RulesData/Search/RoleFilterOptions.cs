namespace Servitor.App.RulesData.Search;

/// <summary>
/// The 18 Role facet chips confirmed live in the reference app's Scry screen
/// (see docs/Reference-App-Analysis.md Section 5).
/// </summary>
public static class RoleFilterOptions
{
    public static readonly IReadOnlyList<string> All =
    [
        "Character", "Epic Hero", "Battleline", "Infantry", "Mounted", "Vehicle",
        "Monster", "Walker", "Fly", "Psyker", "Dedicated Transport", "Fortification",
        "Grenades", "Smoke", "Aircraft", "Titanic", "Beast", "Swarm",
    ];
}
