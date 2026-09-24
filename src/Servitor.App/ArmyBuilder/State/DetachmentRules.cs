using Servitor.RulesData.Domain;

namespace Servitor.App.ArmyBuilder.State;

/// <summary>Which detachments an <see cref="ArmySetup"/> may still take. UI-independent so it is testable on its own.</summary>
public static class DetachmentRules
{
    public static DetachmentAddResult CanAdd(ArmySetup setup, Detachment detachment)
    {
        if (setup.BattleSize is not { } battleSize)
        {
            return DetachmentAddResult.NoBattleSize;
        }

        if (setup.FactionId is null)
        {
            return DetachmentAddResult.NoFaction;
        }

        if (detachment.Mode != GameMode.Standard)
        {
            return DetachmentAddResult.NotStandardGame;
        }

        if (setup.Detachments.Any(d => d.Id == detachment.Id))
        {
            return DetachmentAddResult.AlreadySelected;
        }

        return setup.DetachmentPointsUsed + detachment.DetachmentPoints > battleSize.DetachmentPointsLimit
            ? DetachmentAddResult.ExceedsDetachmentPoints
            : DetachmentAddResult.Added;
    }
}
