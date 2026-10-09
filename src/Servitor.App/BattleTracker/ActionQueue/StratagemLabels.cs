using Servitor.RulesData.Domain;

namespace Servitor.App.BattleTracker.ActionQueue;

public static class StratagemLabels
{
    public static string Category(StratagemCategory category) => category switch
    {
        StratagemCategory.BattleTactic => "Battle Tactic",
        StratagemCategory.StrategicPloy => "Strategic Ploy",
        StratagemCategory.EpicDeed => "Epic Deed",
        StratagemCategory.Wargear => "Wargear",
        StratagemCategory.Other => "Stratagem",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };
}
