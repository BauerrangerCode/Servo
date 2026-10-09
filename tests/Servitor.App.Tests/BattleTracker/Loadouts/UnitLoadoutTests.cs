using Servitor.App.ArmyBuilder.State;
using Servitor.App.BattleTracker.Units;
using Servitor.App.Tests.ArmyBuilder.Catalog;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.BattleTracker.Loadouts;

public class UnitLoadoutTests
{
    private static WeaponProfile Profile(string name, WeaponType type = WeaponType.Ranged) =>
        new(name, type, type == WeaponType.Ranged ? "24\"" : null, "2", "3+", "4", "0", "1", []);

    private static Datasheet Sheet(string name, int wounds, params WeaponProfile[] weapons) =>
        Units.Sheet(name) with
        {
            StatLine = new StatLine("6\"", "5", "3+", null, wounds.ToString(), "7+", "2"),
            RangedWeapons = [.. weapons.Where(w => w.Type == WeaponType.Ranged)],
            MeleeWeapons = [.. weapons.Where(w => w.Type == WeaponType.Melee)],
        };

    private static ArmyUnit Declared(Datasheet sheet, int models, params (string Name, int Count)[] weapons) =>
        new(Guid.NewGuid(), sheet, new UnitSize(models, 100)) { DeclaredWeapons = [.. weapons.Select(w => new DeclaredWeapon(w.Name, w.Count))] };

    private static readonly Datasheet Boyz = Sheet("Boyz", 1,
        Profile("Slugga"), Profile("Shoota"), Profile("➤ Kombi-skorcha - Skorcha"), Profile("➤ Kombi-skorcha - Shoota"),
        Profile("Choppa", WeaponType.Melee), Profile("Kustom Choppa", WeaponType.Melee));

    [Fact]
    public void Weapons_carried_by_every_model_make_a_single_group()
    {
        var loadout = UnitLoadoutBuilder.Build(Declared(Boyz, 10, ("Slugga", 10), ("Choppa", 10)));

        var group = Assert.Single(loadout.Groups);
        Assert.Equal(10, group.Models);
        Assert.Equal(["Slugga", "Choppa"], group.WeaponNames);
    }

    [Fact]
    public void Weapons_with_the_same_count_below_the_unit_size_form_their_own_group()
    {
        var loadout = UnitLoadoutBuilder.Build(Declared(Boyz, 20,
            ("Slugga", 18), ("Choppa", 18), ("Shoota", 18), ("Kustom Choppa", 2), ("Kombi-skorcha", 2)));

        Assert.Equal([18, 2], loadout.Groups.Select(g => g.Models));
        Assert.Equal(["Slugga", "Choppa", "Shoota"], loadout.Groups[0].WeaponNames);
        Assert.Equal(["Kustom Choppa", "Kombi-skorcha"], loadout.Groups[1].WeaponNames);
        Assert.Equal(20, loadout.Models);
    }

    [Fact]
    public void A_shared_weapon_belongs_to_every_group_and_the_models_left_over_form_a_standard_group()
    {
        var loadout = UnitLoadoutBuilder.Build(Declared(Boyz, 10, ("Slugga", 10), ("Choppa", 9), ("Kustom Choppa", 1)));

        Assert.Equal([9, 1], loadout.Groups.Select(g => g.Models));
        Assert.All(loadout.Groups, g => Assert.Contains("Slugga", g.WeaponNames));
        Assert.Equal(10, loadout.Weapons.Single(w => w.Name == "Slugga").Models);
        Assert.Equal(1, loadout.Weapons.Single(w => w.Name == "Kustom Choppa").Models);

        var withRemainder = UnitLoadoutBuilder.Build(Declared(Boyz, 10, ("Slugga", 10), ("Kustom Choppa", 2)));
        Assert.Equal([8, 2], withRemainder.Groups.Select(g => g.Models));
        Assert.Equal("Standard", withRemainder.Groups[0].Label);
    }

    [Fact]
    public void Counts_that_cannot_fit_the_unit_stay_one_group()
    {
        var loadout = UnitLoadoutBuilder.Build(Declared(Boyz, 10, ("Slugga", 10), ("Choppa", 7), ("Kustom Choppa", 6)));

        Assert.Single(loadout.Groups);
        Assert.Equal(10, loadout.Models);
    }

    [Fact]
    public void A_weapon_name_finds_every_profile_of_the_datasheet_and_an_unknown_name_has_none()
    {
        var loadout = UnitLoadoutBuilder.Build(Declared(Boyz, 10, ("Slugga", 10), ("Kombi-skorcha", 10), ("Mystery Gun", 10)));

        Assert.Equal(2, loadout.Weapons.Single(w => w.Name == "Kombi-skorcha").Profiles.Count);
        Assert.Empty(loadout.Weapons.Single(w => w.Name == "Mystery Gun").Profiles);
    }

    [Fact]
    public void Wounds_per_model_come_from_the_datasheet()
    {
        var loadout = UnitLoadoutBuilder.Build(Declared(Sheet("Warboss", 6, Profile("Slugga")), 1, ("Slugga", 1)));

        Assert.Equal(6, Assert.Single(loadout.Groups).WoundsPerModel);
    }

    [Fact]
    public void Without_a_declared_list_the_chosen_wargear_and_the_unoptioned_defaults_are_equipped()
    {
        var sheet = Sheet("Trukk", 10, Profile("Dual Big Shoota"), Profile("Rokkit Launcha"), Profile("Spiked Ram", WeaponType.Melee)) with
        {
            Wargear = new WargearOptions(
                [new WargearModel("Trukk", [new WargearSlot("Dual Big Shoota", ["Dual Big Shoota", "Rokkit Launcha"], 0)])], []),
        };
        var unit = new ArmyUnit(Guid.NewGuid(), sheet, new UnitSize(1, 60));
        var rokkit = unit with
        {
            Wargear = new WargearSelection(new Dictionary<string, int> { ["Trukk|Dual Big Shoota"] = 1 }, new Dictionary<string, int>()),
        };

        Assert.Equal(["Dual Big Shoota", "Spiked Ram"], UnitLoadoutBuilder.Build(unit).Weapons.Select(w => w.Name));
        Assert.Equal(["Rokkit Launcha", "Spiked Ram"], UnitLoadoutBuilder.Build(rokkit).Weapons.Select(w => w.Name));
    }

    [Fact]
    public void A_leader_and_the_unit_it_leads_share_one_card_with_two_roles()
    {
        var leader = new ArmyUnit(Guid.NewGuid(), Sheet("Painboy", 4, Profile("Dok's Toolz", WeaponType.Melee)), new UnitSize(1, 45));
        var boyz = new ArmyUnit(Guid.NewGuid(), Boyz, new UnitSize(10, 90)) { LeaderUnitId = leader.Id };
        var trukk = new ArmyUnit(Guid.NewGuid(), Sheet("Trukk", 10), new UnitSize(1, 60));

        var cards = UnitCards.Build([leader, boyz, trukk]);

        Assert.Equal(["Boyz + Painboy", "Trukk"], cards.Select(c => c.Title));
        Assert.Equal(["Leader", "Bodyguard"], cards[0].Roles.Select(r => r.Label));
        Assert.Equal(["Unit"], cards[1].Roles.Select(r => r.Label));
    }

    [Fact]
    public void Unit_keys_tell_copies_of_a_datasheet_apart_in_army_order()
    {
        var first = new ArmyUnit(Guid.NewGuid(), Boyz, new UnitSize(10, 90));
        var second = new ArmyUnit(Guid.NewGuid(), Boyz, new UnitSize(10, 90));
        IReadOnlyList<ArmyUnit> units = [first, second];

        Assert.Equal(["Boyz#1", "Boyz#2"], units.Select(u => UnitKeys.Of(units, u)));
    }
}

public class GroupWoundsTests
{
    private static readonly ModelGroup Five = new("g0", "Standard", 5, 1, ["Slugga"]);
    private static readonly ModelGroup Tough = new("g0", "Standard", 3, 4, ["Slugga"]);

    [Fact]
    public void Damage_destroys_one_model_per_wounds_per_model_and_hurts_the_next()
    {
        var condition = new GroupCondition(Damage: 9);

        Assert.Equal(2, GroupWounds.Destroyed(Tough, condition));
        Assert.Equal(1, GroupWounds.Alive(Tough, condition));
        Assert.Equal(3, GroupWounds.WoundedModelRemaining(Tough, condition));
        Assert.Equal(3, GroupWounds.WoundsRemaining(Tough, condition));
    }

    [Fact]
    public void Taking_more_wounds_than_a_model_has_destroys_it()
    {
        var onTheLastWound = new GroupCondition(Damage: 3);

        var next = GroupWounds.Adjust(Tough, onTheLastWound, 1);

        Assert.Equal(0, GroupWounds.Destroyed(Tough, onTheLastWound));
        Assert.Equal(1, GroupWounds.Destroyed(Tough, next));
        Assert.Equal(2, GroupWounds.Alive(Tough, next));
    }

    [Fact]
    public void Healing_past_the_maximum_of_the_surviving_model_brings_a_destroyed_one_back()
    {
        var oneLeft = new GroupCondition(Damage: 8);

        var healed = GroupWounds.Adjust(Tough, oneLeft, -1);

        Assert.Equal(1, GroupWounds.Alive(Tough, oneLeft));
        Assert.Equal(2, GroupWounds.Alive(Tough, healed));
        Assert.Equal(1, GroupWounds.WoundedModelRemaining(Tough, healed));
    }

    [Fact]
    public void The_total_stays_between_none_and_the_models_times_their_wounds()
    {
        Assert.Equal(0, GroupWounds.Adjust(Five, GroupCondition.Fresh, -3).Damage);
        Assert.Equal(5, GroupWounds.Adjust(Five, new GroupCondition(Damage: 4), 9).Damage);
        Assert.Equal(12, GroupWounds.MaxDamage(Tough));
        Assert.True(GroupWounds.IsDestroyed(Tough, GroupWounds.Adjust(Tough, GroupCondition.Fresh, 99)));
    }

    [Fact]
    public void Restoring_heals_the_whole_group()
    {
        var gone = GroupWounds.Adjust(Five, GroupCondition.Fresh, 5);

        Assert.True(GroupWounds.IsDestroyed(Five, gone));
        Assert.Equal(0, GroupWounds.Restore(gone).Damage);
    }
}
