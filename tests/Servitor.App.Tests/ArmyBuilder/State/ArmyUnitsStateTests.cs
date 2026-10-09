using Servitor.App.ArmyBuilder.State;
using Servitor.App.Tests.ArmyBuilder.Catalog;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.ArmyBuilder.State;

public class ArmyUnitsStateTests
{
    private static readonly UnitSize Ten = new(10, 90);

    private static ArmyBuilderState Ready(BattleSize? size = null, string faction = "orks")
    {
        var state = new ArmyBuilderState();
        state.SelectBattleSize(size ?? BattleSizes.StrikeForce);
        state.SelectFaction(faction);
        return state;
    }

    [Fact]
    public void Units_cannot_be_added_before_battle_size_and_faction_are_chosen()
    {
        var state = new ArmyBuilderState();
        Assert.Equal(AddUnitResult.NoBattleSize, state.AddUnit(Units.Sheet("Boyz"), Ten));

        state.SelectBattleSize(BattleSizes.StrikeForce);
        Assert.Equal(AddUnitResult.NoFaction, state.AddUnit(Units.Sheet("Boyz"), Ten));
        Assert.Empty(state.Units);
    }

    [Fact]
    public void A_unit_is_added_with_the_chosen_size_and_raises_changed()
    {
        var state = Ready();
        var changes = 0;
        state.Changed += () => changes++;

        Assert.Equal(AddUnitResult.Added, state.AddUnit(Units.Sheet("Boyz"), Ten));

        var unit = Assert.Single(state.Units);
        Assert.Equal("Boyz", unit.Datasheet.Name);
        Assert.Equal(Ten, unit.Size);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void A_unit_of_another_army_is_rejected()
    {
        var state = Ready();

        Assert.Equal(AddUnitResult.NotInFaction, state.AddUnit(Units.Sheet("Cadian", factions: ["astra-militarum"]), Ten));
        Assert.Empty(state.Units);
    }

    [Fact]
    public void A_shared_unit_can_be_taken_by_each_army_that_lists_it()
    {
        var shared = Units.Sheet("Skitarii", factions: ["adeptus-mechanicus", "imperial-knights"]);

        Assert.Equal(AddUnitResult.Added, Ready(faction: "adeptus-mechanicus").AddUnit(shared, Ten));
        Assert.Equal(AddUnitResult.Added, Ready(faction: "imperial-knights").AddUnit(shared, Ten));
    }

    [Fact]
    public void The_per_army_limit_stops_further_copies_and_removing_one_frees_a_slot()
    {
        var state = Ready(BattleSizes.Incursion);
        var boyz = Units.Sheet("Boyz", constraint: new CompositionConstraint(6, 2));

        Assert.Equal(AddUnitResult.Added, state.AddUnit(boyz, Ten));
        Assert.Equal(AddUnitResult.Added, state.AddUnit(boyz, Ten));
        Assert.Equal(AddUnitResult.AtLimit, state.AddUnit(boyz, Ten));
        Assert.Equal(2, UnitRules.CountOf(state.Units, boyz));

        state.RemoveUnit(state.Units[0].Id);

        Assert.Equal(1, UnitRules.CountOf(state.Units, boyz));
        Assert.Equal(AddUnitResult.Added, state.AddUnit(boyz, Ten));
    }

    [Fact]
    public void Datasheets_with_the_same_name_in_different_catalogues_are_counted_apart()
    {
        var state = Ready();
        var epic = new CompositionConstraint(1, null);
        state.AddUnit(Units.Sheet("Boss", constraint: epic, catalogue: "Catalogue A"), Ten);

        Assert.Equal(AddUnitResult.Added, state.AddUnit(Units.Sheet("Boss", constraint: epic, catalogue: "Catalogue B"), Ten));
    }

    [Fact]
    public void Removing_a_unit_leaves_the_others_and_raises_changed()
    {
        var state = Ready();
        state.AddUnit(Units.Sheet("Boyz"), Ten);
        state.AddUnit(Units.Sheet("Warboss"), new UnitSize(1, 80));
        var changes = 0;
        state.Changed += () => changes++;

        state.RemoveUnit(state.Units[0].Id);

        Assert.Equal(["Warboss"], state.Units.Select(u => u.Datasheet.Name));
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Changing_the_faction_discards_the_units()
    {
        var state = Ready();
        state.AddUnit(Units.Sheet("Boyz"), Ten);

        state.SelectFaction("orks");
        Assert.Single(state.Units);

        state.SelectFaction("necrons");
        Assert.Empty(state.Units);
    }
}
