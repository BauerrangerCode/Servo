namespace Servitor.App.Scoring.MissionDeck;

/// <summary>
/// One scoring line of a Secondary Mission. A card can award different VP in Fixed and Tactical mode;
/// a null value means the line does not exist in that mode (e.g. Assassination's per-model lines are Fixed-only).
/// </summary>
public sealed record SecondaryCondition(
    string RoundScope,
    string When,
    string Condition,
    int? FixedPoints,
    int? TacticalPoints,
    ScoringRelation Relation = ScoringRelation.Standalone,
    int FirstRound = ScoringCondition.MinRound,
    int LastRound = ScoringCondition.MaxRound)
{
    public int? PointsIn(SecondaryMode mode) => mode == SecondaryMode.Fixed ? FixedPoints : TacticalPoints;
}

/// <summary>
/// One Secondary Mission card of the Mission Deck 2026-27. <paramref name="WhenDrawn"/> and
/// <paramref name="AdditionalRules"/> are verbatim rules text shown with the card; <paramref name="TurnCap"/> is the
/// "up to N VP" limit printed on the card, shown but not enforced.
/// </summary>
public sealed record SecondaryMission(
    string Name,
    string Legend,
    string WhenDrawn,
    IReadOnlyList<string> AdditionalRules,
    bool FixedEligible,
    int? TurnCap,
    IReadOnlyList<SecondaryCondition> Conditions);
