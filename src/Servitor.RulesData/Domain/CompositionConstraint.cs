namespace Servitor.RulesData.Domain;

/// <summary>
/// Same-datasheet unit limit. <paramref name="IncursionLimit"/> is null when no Incursion-specific
/// override exists in the source data (e.g. Epic Hero units, capped at the same value regardless of Battle Size).
/// </summary>
public sealed record CompositionConstraint(int StrikeForceAndOnslaughtLimit, int? IncursionLimit);
