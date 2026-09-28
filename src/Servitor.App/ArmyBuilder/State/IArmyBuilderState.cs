using Servitor.App.ArmyBuilder.Rules;
using Servitor.RulesData.Domain;

namespace Servitor.App.ArmyBuilder.State;

/// <summary>
/// The army being built. Lives in a scoped service, not in components, so later Build steps, Import and Play share it.
/// </summary>
public interface IArmyBuilderState
{
    event Action? Changed;

    ArmySetup Setup { get; }

    IReadOnlyList<ArmyUnit> Units { get; }

    /// <summary>The detachment rules in force; empty until the rules file is loaded.</summary>
    DetachmentRuleSet Rules { get; }

    void SelectBattleSize(BattleSize battleSize);

    /// <summary>Switching faction discards the selected detachments and the units, which belong to the previous army.</summary>
    /// <param name="allyFactionIds">The factions whose units this faction may take as allies.</param>
    void SelectFaction(string factionId, IReadOnlyList<string>? allyFactionIds = null);

    DetachmentAddResult AddDetachment(Detachment detachment);

    void RemoveDetachment(string detachmentId);

    /// <param name="allyFaction">The ally faction the unit is taken from; null for a unit of the army's own faction.</param>
    AddUnitResult AddUnit(Datasheet datasheet, UnitSize size, string? allyFaction = null);

    void RemoveUnit(Guid unitId);

    void SetWargear(Guid unitId, WargearSelection wargear);

    void SetEnhancement(Guid unitId, Enhancement? enhancement);

    /// <summary>Makes the given unit the army's Warlord, clearing the flag from any previously-set unit.</summary>
    void SetWarlord(Guid unitId);

    /// <param name="leaderUnitId">The leading character's unit id, or null to detach.</param>
    void SetLeader(Guid unitId, Guid? leaderUnitId);

    void SkipSetup();

    void ResumeSetup();
}
