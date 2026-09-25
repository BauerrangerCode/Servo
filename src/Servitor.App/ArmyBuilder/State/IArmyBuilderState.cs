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

    void SelectBattleSize(BattleSize battleSize);

    /// <summary>Switching faction discards the selected detachments and the units, which belong to the previous army.</summary>
    void SelectFaction(string factionId);

    DetachmentAddResult AddDetachment(Detachment detachment);

    void RemoveDetachment(string detachmentId);

    AddUnitResult AddUnit(Datasheet datasheet, UnitSize size);

    void RemoveUnit(Guid unitId);

    void SetWargear(Guid unitId, WargearSelection wargear);

    void SkipSetup();

    void ResumeSetup();
}
