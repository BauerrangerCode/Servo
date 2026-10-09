namespace Servitor.App.Scoring.MissionDeck;

/// <summary>
/// One resolved Primary Mission of the Mission Deck 2026-27 (Chapter Approved). <paramref name="AdditionalRules"/>
/// holds mission-wide setup rules and named "Objective Action" blocks (e.g. Decoy, Sensor Sweep) that define terms
/// used by the scoring conditions but are not themselves scoreable — kept as supporting rules text, verbatim,
/// rather than modelled as an interactive mechanic (out of scope for the setup wizard).
/// </summary>
public sealed record PrimaryMission(string Name, string Legend, IReadOnlyList<ScoringCondition> ScoringConditions, IReadOnlyList<string> AdditionalRules);
