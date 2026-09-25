using Servitor.RulesData.Domain;
using Servitor.RulesData.Mapping;

namespace Servitor.App.Tests.RulesData.Mapping;

public class BsDataWargearMapperTests
{
    private static string C(string type, string scope, int value, string id = "") =>
        $"{{\"id\":\"{id}\",\"field\":\"selections\",\"scope\":\"{scope}\",\"type\":\"{type}\",\"value\":{value}}}";

    private static string Entry(
        string name,
        string? type,
        string[]? constraints = null,
        string[]? children = null,
        string? id = null,
        string? defaultId = null,
        string[]? modifiers = null,
        string[]? links = null)
    {
        var fields = new List<string>
        {
            $"\"id\":\"{id ?? name}\"",
            $"\"name\":\"{name}\"",
        };
        if (type is not null)
        {
            fields.Add($"\"type\":\"{type}\"");
        }

        if (defaultId is not null)
        {
            fields.Add($"\"defaultSelectionEntryId\":\"{defaultId}\"");
        }

        if (constraints is not null)
        {
            fields.Add($"\"constraints\":[{string.Join(",", constraints)}]");
        }

        if (modifiers is not null)
        {
            fields.Add($"\"modifiers\":[{string.Join(",", modifiers)}]");
        }

        if (links is not null)
        {
            fields.Add($"\"entryLinks\":[{string.Join(",", links)}]");
        }

        if (children is not null)
        {
            // Groups and entries share the recursive shape; putting everything under selectionEntries is enough here.
            fields.Add($"\"selectionEntries\":[{string.Join(",", children)}]");
        }

        return "{" + string.Join(",", fields) + "}";
    }

    private static string Link(string name, string targetId, string[]? constraints = null) =>
        $"{{\"id\":\"link-{name}\",\"name\":\"{name}\",\"targetId\":\"{targetId}\",\"type\":\"selectionEntry\""
        + (constraints is null ? "" : $",\"constraints\":[{string.Join(",", constraints)}]") + "}";

    private static string SetIfModels(string constraintId, string op, int threshold, int value, string type = "set") =>
        $"{{\"field\":\"{constraintId}\",\"type\":\"{type}\",\"value\":{value},\"conditions\":[{{\"childId\":\"model\",\"field\":\"selections\",\"scope\":\"unit\",\"type\":\"{op}\",\"value\":{threshold}}}]}}";

    private static Datasheet Map(string unit, string? sharedGroups = null)
    {
        var json = "{\"catalogue\":{\"name\":\"Test\",\"sharedSelectionEntries\":[" + unit + "]"
            + (sharedGroups is null ? "" : ",\"sharedSelectionEntryGroups\":[" + sharedGroups + "]") + "}}";
        var result = BsDataCatalogueMapper.Map(json);
        Assert.Empty(result.Warnings);
        return Assert.Single(result.Datasheets);
    }

    private static string WeaponGroup(string name, string[] choices, string? defaultId = null) =>
        Entry(name, null, [C("min", "parent", 1), C("max", "parent", 1)], choices.Select(c => Entry(c, "upgrade")).ToArray(), defaultId: defaultId);

    // ---- weapon slots of unique models --------------------------------------------------------------------------

    [Fact]
    public void A_single_model_unit_gets_a_slot_for_each_group_of_which_one_choice_is_taken()
    {
        var unit = Entry("Farseer", "model", children:
        [
            Entry("Wargear", null, children: [WeaponGroup("Weapon", ["Witchblade", "Singing Spear"], defaultId: "Singing Spear")]),
        ]);

        var wargear = Map(unit).Wargear;

        var model = Assert.Single(wargear!.Models);
        Assert.Equal("Farseer", model.ModelName);
        var slot = Assert.Single(model.Slots);
        Assert.Equal("Weapon", slot.Name);
        Assert.Equal(["Witchblade", "Singing Spear"], slot.Choices);
        Assert.Equal(1, slot.DefaultIndex);
        Assert.Empty(wargear.Pools);
    }

    [Fact]
    public void Without_a_default_the_first_choice_is_the_default()
    {
        var unit = Entry("Captain", "model", children: [WeaponGroup("Weapon", ["Bolter", "Chainsword"])]);

        Assert.Equal(0, Map(unit).Wargear!.Models[0].Slots[0].DefaultIndex);
    }

    [Fact]
    public void A_default_that_names_the_choices_link_target_is_found()
    {
        var group = Entry(
            "Weapon",
            null,
            [C("min", "parent", 1), C("max", "parent", 1)],
            defaultId: "target-b",
            links: [Link("Bolter", "target-a"), Link("Chainsword", "target-b")]);
        var unit = Entry("Captain", "model", children: [group]);

        var json = "{\"catalogue\":{\"name\":\"Test\",\"sharedSelectionEntries\":[" + unit + ",{\"id\":\"target-a\",\"name\":\"Bolter\",\"type\":\"upgrade\"},{\"id\":\"target-b\",\"name\":\"Chainsword\",\"type\":\"upgrade\"}]}}";
        var slot = BsDataCatalogueMapper.Map(json).Datasheets.Single(d => d.Name == "Captain").Wargear!.Models[0].Slots[0];

        Assert.Equal(["Bolter", "Chainsword"], slot.Choices);
        Assert.Equal(1, slot.DefaultIndex);
    }

    [Fact]
    public void A_group_the_unit_reaches_through_an_entry_link_to_a_shared_group_is_read()
    {
        var shared = Entry("Weapon", null, [C("min", "parent", 1), C("max", "parent", 1)], [Entry("Bolter", "upgrade"), Entry("Chainsword", "upgrade")], id: "shared-weapon");
        var unit = Entry("Captain", "model", links: [Link("Weapon", "shared-weapon")]);

        Assert.Equal(["Bolter", "Chainsword"], Map(unit, shared).Wargear!.Models[0].Slots[0].Choices);
    }

    [Fact]
    public void The_limits_of_an_entry_link_count_for_the_linked_group()
    {
        var shared = Entry("Weapon", null, children: [Entry("Bolter", "upgrade"), Entry("Chainsword", "upgrade")], id: "shared-weapon");
        var unit = Entry("Captain", "model", links: [Link("Weapon", "shared-weapon", [C("min", "parent", 1), C("max", "parent", 1)])]);

        Assert.NotNull(Map(unit, shared).Wargear);
    }

    [Fact]
    public void A_group_with_loadout_packages_that_have_options_of_their_own_is_not_offered()
    {
        var package = Entry("Package", "upgrade", children: [WeaponGroup("Melee", ["Sword", "Axe"])]);
        var unit = Entry("Captain", "model", children:
        [
            Entry("Wargear", null, [C("min", "parent", 1), C("max", "parent", 1)], [package, Entry("Other package", "upgrade")]),
        ]);

        Assert.Null(Map(unit).Wargear);
    }

    [Fact]
    public void A_group_with_a_single_choice_or_without_an_exactly_one_limit_is_no_slot()
    {
        var optional = Entry("Extras", null, [C("min", "parent", 0), C("max", "parent", 1)], [Entry("A", "upgrade"), Entry("B", "upgrade")]);
        var unit = Entry("Captain", "model", children: [WeaponGroup("Only", ["Bolter"]), optional]);

        Assert.Null(Map(unit).Wargear);
    }

    [Fact]
    public void Crusade_and_enhancement_entries_are_not_wargear()
    {
        var unit = Entry("Captain", "model", children:
        [
            WeaponGroup("Crusade Weapons", ["A", "B"]),
            Entry("Enhancements", null, [C("min", "parent", 1), C("max", "parent", 1)], [Entry("X", "upgrade"), Entry("Y", "upgrade")]),
        ]);

        Assert.Null(Map(unit).Wargear);
    }

    [Fact]
    public void Only_the_unique_model_of_a_squad_gets_slots_the_regular_models_do_not()
    {
        var unit = Entry("Squad", "unit", children:
        [
            Entry("Squad models", null, [C("min", "parent", 5), C("max", "parent", 10)], children:
            [
                Entry("Sergeant", "model", [C("min", "parent", 1), C("max", "parent", 1)], [WeaponGroup("Weapon 1", ["Pistol", "Plasma pistol"])]),
                Entry("Trooper", "model", [C("min", "parent", 4), C("max", "parent", 9)], [WeaponGroup("Weapon", ["Rifle", "Carbine"])]),
            ]),
        ]);

        var wargear = Map(unit).Wargear;

        Assert.Equal(["Sergeant"], wargear!.Models.Select(m => m.ModelName));
    }

    [Fact]
    public void A_unit_without_options_has_no_wargear()
    {
        Assert.Null(Map(Entry("Rhino", "model", children: [Entry("Storm bolter", "upgrade")])).Wargear);
    }

    // ---- pools -------------------------------------------------------------------------------------------------

    private static string BoyzUnit() => Entry("Boyz", "unit", children:
    [
        Entry("9-18 Boyz", null, [C("min", "parent", 9), C("max", "parent", 18)], children:
        [
            Entry("Boy", "model", [C("min", "parent", 6), C("max", "parent", 18)], [Entry("Choppa", "upgrade"), Entry("Slugga", "upgrade"), Entry("Shoota", "upgrade")]),
            Entry("Special Weapons", null, [C("max", "parent", 3, "group-max")], modifiers: [SetIfModels("group-max", "atLeast", 20, 3, "increment")], children:
            [
                Entry(
                    "Boy w/ Big shoota",
                    "model",
                    [C("max", "parent", 2)],
                    [
                        Entry("Big Shoota", "upgrade", [C("max", "parent", 1), C("max", "unit", 1, "shoota-unit-max")], modifiers: [SetIfModels("shoota-unit-max", "atLeast", 20, 2)]),
                        Entry("Choppa", "upgrade"),
                        Entry("Slugga", "upgrade"),
                    ]),
            ]),
        ]),
    ]);

    [Fact]
    public void A_model_variant_becomes_a_pool_for_the_weapon_only_it_has_replacing_the_one_only_the_regular_models_have()
    {
        var pool = Assert.Single(Map(BoyzUnit()).Wargear!.Pools);

        Assert.Equal("Big Shoota", pool.Item);
        Assert.Equal("Shoota", pool.Replaces);
    }

    [Fact]
    public void The_pool_cap_is_the_smallest_limit_and_follows_the_unit_size()
    {
        var pool = Assert.Single(Map(BoyzUnit()).Wargear!.Pools);

        Assert.Equal(1, WargearLimits.MaxFor(pool, 10));
        Assert.Equal(2, WargearLimits.MaxFor(pool, 20));
    }

    [Fact]
    public void The_group_that_holds_the_regular_models_is_not_a_limit_of_the_pool()
    {
        var pool = Assert.Single(Map(BoyzUnit()).Wargear!.Pools);

        Assert.DoesNotContain(pool.Limits, l => l.Base == 18);
        Assert.Equal(3, pool.Limits.Count); // per model, per unit, "Special Weapons" group
    }

    [Fact]
    public void A_variant_that_only_differs_by_name_takes_its_item_from_the_model_name()
    {
        var unit = Entry("Squad", "unit", children:
        [
            Entry("Troopers", "model", [C("min", "parent", 4), C("max", "parent", 9)], [Entry("Rifle", "upgrade")]),
            Entry("Trooper w/ Flamer", "model", [C("max", "parent", 2)], [Entry("Rifle", "upgrade")]),
        ]);

        var pool = Assert.Single(Map(unit).Wargear!.Pools);

        Assert.Equal("Flamer", pool.Item);
        Assert.Null(pool.Replaces);
        Assert.Equal(2, WargearLimits.MaxFor(pool, 10));
    }

    [Fact]
    public void A_modifier_with_a_condition_that_is_not_about_the_model_count_is_ignored()
    {
        var other = "{\"field\":\"cap\",\"type\":\"set\",\"value\":9,\"conditions\":[{\"childId\":\"some-roster-entry\",\"field\":\"selections\",\"scope\":\"roster\",\"type\":\"atLeast\",\"value\":1}]}";
        var unit = Entry("Squad", "unit", children:
        [
            Entry("Troopers", "model", [C("min", "parent", 4), C("max", "parent", 9)], [Entry("Rifle", "upgrade")]),
            Entry("Trooper w/ Flamer", "model", [C("max", "parent", 2, "cap")], [Entry("Rifle", "upgrade")], modifiers: [other]),
        ]);

        var pool = Assert.Single(Map(unit).Wargear!.Pools);

        Assert.Equal(2, WargearLimits.MaxFor(pool, 20));
    }

    // ---- real catalogues ---------------------------------------------------------------------------------------

    private static Datasheet Real(string file, string name)
    {
        var result = BsDataCatalogueMapper.Map(File.ReadAllText(TestPaths.WwwrootDataFile("bsdata", "wh40k-11e", file)));
        return result.Datasheets.First(d => d.Name == name);
    }

    [Fact]
    public void Real_boyz_can_take_special_weapons_and_more_of_them_in_a_bigger_unit()
    {
        var pools = Real("Orks.json", "Boyz").Wargear!.Pools;

        Assert.Equal(["Big Shoota", "Rokkit Launcha", "Burna"], pools.Select(p => p.Item));
        Assert.All(pools, p => Assert.Equal("Shoota", p.Replaces));
        Assert.All(pools, p => Assert.True(WargearLimits.MaxFor(p, 20) > WargearLimits.MaxFor(p, 10)));
    }

    [Fact]
    public void A_real_sergeant_has_two_weapon_slots_with_their_default()
    {
        var model = Real("Imperium - Space Marines.json", "Assault Intercessor Squad").Wargear!.Models.Single();

        Assert.Equal("Assault Intercessor Sergeant", model.ModelName);
        Assert.Equal(["Weapon 1", "Weapon 2"], model.Slots.Select(s => s.Name));
        Assert.Equal("Heavy Bolt Pistol", model.Slots[0].Choices[model.Slots[0].DefaultIndex]);
    }

    [Fact]
    public void A_real_character_has_a_weapon_slot_and_a_vehicle_without_options_has_none()
    {
        Assert.Equal("Weapon", Real("Aeldari - Aeldari Library.json", "Farseer").Wargear!.Models.Single().Slots.Single().Name);
        Assert.Null(Real("Imperium - Space Marines.json", "Rhino").Wargear);
    }

    [Fact]
    public void A_good_share_of_the_real_units_have_wargear_options()
    {
        var withOptions = Directory.GetFiles(TestPaths.WwwrootDataFile("bsdata", "wh40k-11e"), "*.json")
            .Where(f => !f.EndsWith("Warhammer 40,000.json", StringComparison.Ordinal))
            .SelectMany(f => BsDataCatalogueMapper.Map(File.ReadAllText(f)).Datasheets)
            .Count(d => d.Wargear is not null);

        Assert.InRange(withOptions, 400, 1000);
    }
}
