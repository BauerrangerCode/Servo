using Servitor.RulesData.Domain;

namespace Servitor.App.ArmyBuilder.State;

/// <summary>
/// The army being built. Lives in a scoped service, not in components, so later Build steps, Import and Play share it.
/// </summary>
public interface IArmyBuilderState
{
    event Action? Changed;

    ArmySetup Setup { get; }

    void SelectBattleSize(BattleSize battleSize);

    /// <summary>Switching faction discards the selected detachments, which belong to the previous one.</summary>
    void SelectFaction(string factionId);

    DetachmentAddResult AddDetachment(Detachment detachment);

    void RemoveDetachment(string detachmentId);

    void SkipSetup();

    void ResumeSetup();
}
