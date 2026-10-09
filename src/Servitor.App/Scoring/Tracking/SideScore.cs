namespace Servitor.App.Scoring.Tracking;

/// <summary>
/// One side's VP after the Chapter Approved limits. <paramref name="PrimaryRounds"/> and
/// <paramref name="SecondaryRounds"/> hold rounds 1-5 (index 0-4), each capped at 15VP;
/// <paramref name="PrimaryEndOfBattle"/> is outside the per-round limit. <paramref name="Primary"/> and
/// <paramref name="Secondary"/> are the capped totals (45VP each).
/// </summary>
public sealed record SideScore(
    IReadOnlyList<int> PrimaryRounds,
    int PrimaryEndOfBattle,
    int Primary,
    IReadOnlyList<int> SecondaryRounds,
    int Secondary,
    int Paint,
    int Challenger)
{
    public int Total => Primary + Secondary + Paint + Challenger;
}
