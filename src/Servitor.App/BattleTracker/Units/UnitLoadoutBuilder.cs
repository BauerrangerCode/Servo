using Servitor.App.ArmyBuilder.State;
using Servitor.RulesData.Domain;

namespace Servitor.App.BattleTracker.Units;

/// <summary>
/// Works out what a unit is equipped with and splits its models into groups of identical equipment. The weapon counts
/// come from the imported list when there is one; otherwise they are derived from the datasheet and the Build wargear
/// choices, which is an approximation because the datasheet does not state the models' default equipment.
/// </summary>
public static class UnitLoadoutBuilder
{
    private const string BundleSeparator = " + ";
    private const string SharedLabel = "Standard";

    public static UnitLoadout Build(ArmyUnit unit)
    {
        var models = Math.Max(1, unit.Size.Models);
        var counts = unit.DeclaredWeapons is { Count: > 0 } declared ? FromDeclared(declared, models) : Derive(unit, models);
        if (counts.Count == 0)
        {
            counts = WeaponNames.AllProfiles(unit.Datasheet).Select(p => WeaponNames.BaseName(p.Name)).Distinct().ToDictionary(n => n, _ => models);
        }

        var groups = Group(counts, models, unit.Datasheet);
        var weapons = counts.Keys.Select(name => new EquippedWeapon(name, groups.Where(g => g.WeaponNames.Contains(name)).Sum(g => g.Models), WeaponNames.ProfilesOf(unit.Datasheet, name))).ToList();
        return new UnitLoadout(groups, weapons);
    }

    private static Dictionary<string, int> FromDeclared(IReadOnlyList<DeclaredWeapon> declared, int models)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var weapon in declared)
        {
            counts[weapon.Name] = Math.Min(models, counts.GetValueOrDefault(weapon.Name) + weapon.Count);
        }

        return counts;
    }

    private static Dictionary<string, int> Derive(ArmyUnit unit, int models)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var options = unit.Datasheet.Wargear;
        var optionKeys = new HashSet<string>();

        if (options is not null)
        {
            foreach (var model in options.Models)
            {
                foreach (var slot in model.Slots)
                {
                    var chosen = slot.Choices[WargearConfig.ChoiceOf(unit, model, slot)];
                    Add(counts, chosen, 1);
                    slot.Choices.ToList().ForEach(c => optionKeys.Add(WeaponNames.Key(c)));
                }
            }

            foreach (var pool in options.Pools)
            {
                var taken = WargearConfig.CountOf(unit, pool);
                foreach (var part in pool.Item.Split(BundleSeparator, StringSplitOptions.TrimEntries))
                {
                    optionKeys.Add(WeaponNames.Key(part));
                    if (taken > 0)
                    {
                        Add(counts, part, taken);
                    }
                }

                if (pool.Replaces is { } replaced)
                {
                    optionKeys.Add(WeaponNames.Key(replaced));
                }
            }

            foreach (var replaced in options.Pools.Where(p => p.Replaces is not null).GroupBy(p => p.Replaces!))
            {
                var remainder = models - replaced.Sum(p => WargearConfig.CountOf(unit, p));
                if (remainder > 0)
                {
                    Add(counts, replaced.Key, remainder);
                }
            }
        }

        var unique = options?.Models.Count ?? 0;
        foreach (var name in WeaponNames.AllProfiles(unit.Datasheet).Select(p => WeaponNames.BaseName(p.Name)).Distinct())
        {
            if (!optionKeys.Contains(WeaponNames.Key(name)))
            {
                Add(counts, name, Math.Max(1, models - unique));
            }
        }

        return counts;
    }

    private static void Add(Dictionary<string, int> counts, string name, int count)
    {
        var existing = counts.Keys.FirstOrDefault(k => WeaponNames.Key(k) == WeaponNames.Key(name)) ?? name;
        counts[existing] = counts.GetValueOrDefault(existing) + count;
    }

    // Weapons carried by every model are shared; the others are grouped by how many models carry them. Weapons with the
    // same count are taken to sit on the same models, and the groups must fit into the unit, else it stays one group.
    private static IReadOnlyList<ModelGroup> Group(Dictionary<string, int> counts, int models, Datasheet datasheet)
    {
        var wounds = WoundsPerModel(datasheet);
        var shared = counts.Where(c => c.Value >= models).Select(c => c.Key).ToList();
        var partial = counts.Where(c => c.Value < models).GroupBy(c => c.Value).OrderByDescending(g => g.Key).ToList();

        if (partial.Count == 0 || partial.Sum(g => g.Key) > models)
        {
            return [new ModelGroup("g0", SharedLabel, models, wounds, [.. counts.Keys])];
        }

        var groups = new List<ModelGroup>();
        var remainder = models - partial.Sum(g => g.Key);
        if (remainder > 0)
        {
            groups.Add(new ModelGroup("g0", SharedLabel, remainder, wounds, shared));
        }

        foreach (var group in partial)
        {
            var names = group.Select(c => c.Key).ToList();
            groups.Add(new ModelGroup($"g{groups.Count}", string.Join(", ", names), group.Key, wounds, [.. shared, .. names]));
        }

        return groups;
    }

    public static int WoundsPerModel(Datasheet datasheet) =>
        int.TryParse(datasheet.StatLine.Wounds, out var wounds) && wounds > 0 ? wounds : 1;
}
