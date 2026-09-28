using Servitor.RulesData.Domain;

namespace Servitor.App.ArmyBuilder.State;

/// <summary>
/// The first build step: Battle Size, faction and detachments. <paramref name="IsSkipped"/> records that the player
/// chose to proceed without finalizing this step.
/// </summary>
public sealed record ArmySetup(
    BattleSize? BattleSize,
    string? FactionId,
    IReadOnlyList<Detachment> Detachments,
    bool IsSkipped)
{
    public static ArmySetup Empty { get; } = new(null, null, [], false);

    /// <summary>The factions (rules-text slugs) whose units the chosen faction may take as allies.</summary>
    public IReadOnlyList<string> AllyFactionIds { get; init; } = [];

    public int DetachmentPointsUsed => Detachments.Sum(d => d.DetachmentPoints);
}
