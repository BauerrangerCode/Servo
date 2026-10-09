namespace Servitor.RulesData.Domain;

/// <summary>
/// The sizes and prices that apply to the <paramref name="FirstCopy"/>-th up to the <paramref name="LastCopy"/>-th
/// unit of one datasheet in an army (LastCopy null: every further copy). A datasheet without price escalation has
/// one bracket covering all copies.
/// </summary>
public sealed record CostBracket(int FirstCopy, int? LastCopy, IReadOnlyList<UnitSize> Sizes)
{
    public bool Covers(int copy) => copy >= FirstCopy && (LastCopy is null || copy <= LastCopy);
}
