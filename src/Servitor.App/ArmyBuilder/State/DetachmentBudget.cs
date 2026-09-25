using Servitor.RulesData.Domain;

namespace Servitor.App.ArmyBuilder.State;

/// <summary>The detachment points (DP) an army may spend on detachments.</summary>
public static class DetachmentBudget
{
    /// <summary>The Core Rules let an Incursion army take a single 3 DP detachment although the battle size gives only 2 DP.</summary>
    private const int IncursionSoloDetachmentPoints = 3;

    public static int Used(IEnumerable<Detachment> detachments) => detachments.Sum(d => d.DetachmentPoints);

    public static bool IsWithinBudget(BattleSize battleSize, IReadOnlyList<Detachment> detachments)
    {
        var used = Used(detachments);
        if (used <= battleSize.DetachmentPointsLimit)
        {
            return true;
        }

        return battleSize == BattleSizes.Incursion && detachments.Count == 1 && used == IncursionSoloDetachmentPoints;
    }
}
