using Servitor.App.ArmyBuilder.Rules;
using Servitor.RulesData.Domain;

namespace Servitor.App.ArmyBuilder.State;

public sealed class ArmyBuilderState(IDetachmentRuleProvider? ruleProvider = null) : IArmyBuilderState
{
    public event Action? Changed;

    public ArmySetup Setup { get; private set; } = ArmySetup.Empty;

    public IReadOnlyList<ArmyUnit> Units { get; private set; } = [];

    public DetachmentRuleSet Rules => ruleProvider?.Current ?? DetachmentRuleSet.Empty;

    public void SelectBattleSize(BattleSize battleSize) => Update(Setup with { BattleSize = battleSize });

    public void SelectFaction(string factionId, IReadOnlyList<string>? allyFactionIds = null)
    {
        var allies = allyFactionIds ?? [];
        if (factionId == Setup.FactionId)
        {
            if (!allies.SequenceEqual(Setup.AllyFactionIds))
            {
                Update(Setup with { AllyFactionIds = allies });
            }

            return;
        }

        Units = [];
        Update(Setup with { FactionId = factionId, Detachments = [], AllyFactionIds = allies });
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

    public AddUnitResult AddUnit(Datasheet datasheet, UnitSize size, string? allyFaction = null)
    {
        var result = UnitRules.CanAdd(Setup, Units, datasheet, Rules, allyFaction);
        if (result == AddUnitResult.Added)
        {
            Units = [.. Units, new ArmyUnit(Guid.NewGuid(), datasheet, size) { AlliedFaction = allyFaction }];
            Changed?.Invoke();
        }

        return result;
    }

    public void SetWargear(Guid unitId, WargearSelection wargear)
    {
        Units = Units.Select(u => u.Id == unitId ? u with { Wargear = wargear } : u).ToList();
        Changed?.Invoke();
    }

    public void SetEnhancement(Guid unitId, Enhancement? enhancement)
    {
        Units = Units.Select(u => u.Id == unitId ? u with { Enhancement = enhancement } : u).ToList();
        Changed?.Invoke();
    }

    public void RemoveUnit(Guid unitId)
    {
        Units = Units.Where(u => u.Id != unitId).ToList();
        Changed?.Invoke();
    }

    public void SkipSetup() => Update(Setup with { IsSkipped = true });

    public void ResumeSetup() => Update(Setup with { IsSkipped = false });

    private void Update(ArmySetup setup)
    {
        Setup = setup;
        Changed?.Invoke();
    }
}
