using Servitor.App.ArmyBuilder.State;
using Servitor.App.ArmyCollection.State;

namespace Servitor.App.ArmyCollection.Resolution;

/// <summary>Captures the army currently held in <see cref="IArmyBuilderState"/> as a <see cref="SavedArmy"/>.</summary>
public static class ArmySnapshot
{
    /// <summary>
    /// Requires a faction and battle size to already be chosen — Build only offers "Save to My Collection" once
    /// both are set, so this is the state's own precondition, not a new one.
    /// </summary>
    public static SavedArmy Capture(string name, IArmyBuilderState state)
    {
        var setup = state.Setup;
        if (setup.FactionId is not { } factionId || setup.BattleSize is not { } battleSize)
        {
            throw new InvalidOperationException("Cannot save an army before a faction and battle size are chosen.");
        }

        return new SavedArmy(
            Guid.NewGuid(),
            name,
            DateTimeOffset.UtcNow,
            factionId,
            battleSize.Name,
            setup.AllyFactionIds,
            [.. setup.Detachments.Select(d => d.Name)],
            [.. state.Units.Select(ToSavedUnit)]);
    }

    private static SavedUnit ToSavedUnit(ArmyUnit unit) => new(
        unit.Id,
        unit.Datasheet.Name,
        unit.Size.Models,
        unit.Wargear,
        unit.Enhancement?.Name,
        unit.AlliedFaction,
        unit.IsWarlord,
        unit.LeaderUnitId);
}
