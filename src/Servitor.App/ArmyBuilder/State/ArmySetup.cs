using Servitor.RulesData.Domain;

namespace Servitor.App.ArmyBuilder.State;

/// <summary>The first build step: Battle Size, faction and detachments.</summary>
public sealed record ArmySetup(
    BattleSize? BattleSize,
    string? FactionId,
    IReadOnlyList<Detachment> Detachments)
{
    public static ArmySetup Empty { get; } = new(null, null, []);

    /// <summary>The factions (rules-text slugs) whose units the chosen faction may take as allies.</summary>
    public IReadOnlyList<string> AllyFactionIds { get; init; } = [];

    public int DetachmentPointsUsed => Detachments.Sum(d => d.DetachmentPoints);
}
