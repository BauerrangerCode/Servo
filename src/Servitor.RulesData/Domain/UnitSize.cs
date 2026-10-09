namespace Servitor.RulesData.Domain;

/// <summary>
/// One way to field a unit: <paramref name="Models"/> models for <paramref name="Points"/> points. Models is 0 when
/// the source does not say how many models the points are for.
/// </summary>
public sealed record UnitSize(int Models, int Points);
