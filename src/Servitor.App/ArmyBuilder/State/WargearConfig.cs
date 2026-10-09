using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.ArmyBuilder.State;

/// <summary>Reads and changes a unit's wargear selection against the wargear options of its datasheet. UI-independent.</summary>
public static class WargearConfig
{
    /// <summary>Whether the unit has anything to configure; a unit without wargear options shows no wargear entry point.</summary>
    public static bool HasOptions(Datasheet datasheet) => datasheet.Wargear is { IsEmpty: false };

    public static string SlotKey(WargearModel model, WargearSlot slot) => $"{model.ModelName}|{slot.Name}";

    /// <summary>The index of the choice the slot currently has: the picked one, else the slot's default.</summary>
    public static int ChoiceOf(ArmyUnit unit, WargearModel model, WargearSlot slot)
    {
        var index = unit.Wargear.SlotChoices.TryGetValue(SlotKey(model, slot), out var picked) ? picked : slot.DefaultIndex;
        return index >= 0 && index < slot.Choices.Count ? index : slot.DefaultIndex;
    }

    /// <summary>How many models of the unit currently take the pool's item, never above what the unit's size allows.</summary>
    public static int CountOf(ArmyUnit unit, WargearPool pool) =>
        Math.Min(unit.Wargear.PoolCounts.GetValueOrDefault(pool.Item), MaxOf(unit, pool));

    /// <summary>How many models of the unit may take the pool's item at the unit's size.</summary>
    public static int MaxOf(ArmyUnit unit, WargearPool pool) => WargearLimits.MaxFor(pool, unit.Size.Models);

    public static WargearSelection Choose(ArmyUnit unit, WargearModel model, WargearSlot slot, int choiceIndex)
    {
        var choices = new Dictionary<string, int>(unit.Wargear.SlotChoices);
        choices[SlotKey(model, slot)] = Math.Clamp(choiceIndex, 0, slot.Choices.Count - 1);
        return unit.Wargear with { SlotChoices = choices };
    }

    public static WargearSelection SetCount(ArmyUnit unit, WargearPool pool, int count)
    {
        var counts = new Dictionary<string, int>(unit.Wargear.PoolCounts)
        {
            [pool.Item] = Math.Clamp(count, 0, MaxOf(unit, pool)),
        };
        return unit.Wargear with { PoolCounts = counts };
    }

    /// <summary>The one-line loadout: the chosen weapon of every slot and the pool items models take ("2× Big Shoota").</summary>
    public static string Loadout(ArmyUnit unit)
    {
        if (unit.Datasheet.Wargear is not { } options)
        {
            return "";
        }

        var parts = options.Models
            .SelectMany(model => model.Slots.Select(slot => slot.Choices[ChoiceOf(unit, model, slot)]))
            .Concat(options.Pools.Where(p => CountOf(unit, p) > 0).Select(p => $"{CountOf(unit, p)}× {p.Item}"));
        return string.Join(", ", parts);
    }

    /// <summary>The points the chosen wargear adds, from the datasheet's per-item prices (items without a price add nothing).</summary>
    public static int Points(ArmyUnit unit)
    {
        if (unit.Datasheet.Wargear is not { } options)
        {
            return 0;
        }

        var chosen = options.Models
            .SelectMany(model => model.Slots.Select(slot => PriceOf(unit.Datasheet, slot.Choices[ChoiceOf(unit, model, slot)])));
        var pooled = options.Pools.Select(pool => CountOf(unit, pool) * PriceOf(unit.Datasheet, pool.Item));
        return chosen.Sum() + pooled.Sum();
    }

    /// <summary>What one item costs, or 0 when the datasheet lists no price for it. Names are compared the way the merge compares them.</summary>
    public static int PriceOf(Datasheet datasheet, string item)
    {
        var key = NameNormalizer.Normalize(item);
        return datasheet.WargearCosts.FirstOrDefault(c => NameNormalizer.Normalize(c.Item) == key)?.Points ?? 0;
    }
}
