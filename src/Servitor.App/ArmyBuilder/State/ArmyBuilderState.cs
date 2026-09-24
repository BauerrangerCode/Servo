using Servitor.RulesData.Domain;

namespace Servitor.App.ArmyBuilder.State;

public sealed class ArmyBuilderState : IArmyBuilderState
{
    public event Action? Changed;

    public ArmySetup Setup { get; private set; } = ArmySetup.Empty;

    public void SelectBattleSize(BattleSize battleSize) => Update(Setup with { BattleSize = battleSize });

    public void SelectFaction(string factionId)
    {
        if (factionId == Setup.FactionId)
        {
            return;
        }

        Update(Setup with { FactionId = factionId, Detachments = [] });
    }

    public DetachmentAddResult AddDetachment(Detachment detachment)
    {
        var result = DetachmentRules.CanAdd(Setup, detachment);
        if (result == DetachmentAddResult.Added)
        {
            Update(Setup with { Detachments = [.. Setup.Detachments, detachment] });
        }

        return result;
    }

    public void RemoveDetachment(string detachmentId) =>
        Update(Setup with { Detachments = Setup.Detachments.Where(d => d.Id != detachmentId).ToList() });

    public void SkipSetup() => Update(Setup with { IsSkipped = true });

    public void ResumeSetup() => Update(Setup with { IsSkipped = false });

    private void Update(ArmySetup setup)
    {
        Setup = setup;
        Changed?.Invoke();
    }
}
