namespace Servitor.App.Scoring.MissionDeck;

/// <summary>
/// How a <see cref="ScoringCondition"/> combines with the one immediately before it under the same
/// <see cref="ScoringCondition.RoundScope"/>/<see cref="ScoringCondition.When"/>: <see cref="Standalone"/> conditions
/// each score independently, <see cref="Or"/> conditions are alternatives (the source text says "OR"), and
/// <see cref="Cumulative"/> conditions are a bonus added on top of the previous one (the source text says "CUMULATIVE").
/// </summary>
public enum ScoringRelation
{
    Standalone,
    Or,
    Cumulative,
}

/// <summary>
/// One line of a Primary Mission's "how to score" list, e.g. "Second Battle Round Onwards / End of your Command
/// phase… / For each objective you control. / 3VP". <paramref name="When"/> is empty for an "End of the Battle"
/// condition, whose timing is fully described by <paramref name="RoundScope"/> alone.
/// </summary>
public sealed record ScoringCondition(string RoundScope, string When, string Condition, int Points, ScoringRelation Relation = ScoringRelation.Standalone);
