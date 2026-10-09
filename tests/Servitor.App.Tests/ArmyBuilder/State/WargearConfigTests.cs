using Servitor.App.ArmyBuilder.State;
using Servitor.App.Tests.ArmyBuilder.Catalog;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.ArmyBuilder.State;

public class WargearConfigTests
{
    private static readonly WargearSlot Weapon = new("Weapon", ["Bolt rifle", "Plasma pistol", "Power fist"], DefaultIndex: 0);
    private static readonly WargearModel Sergeant = new("Sergeant", [Weapon]);

    // Up to 1 model in a 10-model unit and up to 2 in a 20-model unit may take it.
    private static readonly WargearPool BigShoota = new(
        "Big Shoota",
        "Shoota",
        [new WargearLimit(1, [new LimitAdjustment(true, 2, [new ModelCountCondition(ModelCountOperator.AtLeast, 20)])])]);

    private static ArmyUnit Unit(int models = 10, WargearCost[]? costs = null, WargearSelection? selection = null)
    {
        var datasheet = Units.Sheet("Squad", sizes: [new UnitSize(models, 100)]) with
        {
            Wargear = new WargearOptions([Sergeant], [BigShoota]),
            WargearCosts = costs ?? [],
        };
        return new ArmyUnit(Guid.NewGuid(), datasheet, new UnitSize(models, 100)) { Wargear = selection ?? WargearSelection.Default };
    }

    [Fact]
    public void A_unit_with_wargear_options_has_something_to_configure_and_one_without_has_not()
    {
        Assert.True(WargearConfig.HasOptions(Unit().Datasheet));
        Assert.False(WargearConfig.HasOptions(Units.Sheet("Rhino")));
        Assert.False(WargearConfig.HasOptions(Units.Sheet("Empty") with { Wargear = new WargearOptions([], []) }));
    }

    [Fact]
    public void Everything_starts_at_the_default()
    {
        var unit = Unit();

        Assert.Equal(0, WargearConfig.ChoiceOf(unit, Sergeant, Weapon));
        Assert.Equal(0, WargearConfig.CountOf(unit, BigShoota));
        Assert.Equal("Bolt rifle", WargearConfig.Loadout(unit));
        Assert.Equal(0, WargearConfig.Points(unit));
    }

    [Fact]
    public void Choosing_a_weapon_changes_the_slot_and_leaves_the_original_selection_alone()
    {
        var unit = Unit();

        var selection = WargearConfig.Choose(unit, Sergeant, Weapon, 2);

        Assert.Equal(2, WargearConfig.ChoiceOf(unit with { Wargear = selection }, Sergeant, Weapon));
        Assert.Equal(0, WargearConfig.ChoiceOf(unit, Sergeant, Weapon));
    }

    [Fact]
    public void A_choice_outside_the_list_is_kept_within_it()
    {
        var unit = Unit();

        Assert.Equal(2, WargearConfig.ChoiceOf(unit with { Wargear = WargearConfig.Choose(unit, Sergeant, Weapon, 99) }, Sergeant, Weapon));
        Assert.Equal(0, WargearConfig.ChoiceOf(unit with { Wargear = WargearConfig.Choose(unit, Sergeant, Weapon, -3) }, Sergeant, Weapon));
    }

    [Theory]
    [InlineData(10, 1)]
    [InlineData(20, 2)]
    public void The_number_of_models_taking_an_item_is_capped_by_the_unit_size(int models, int max)
    {
        var unit = Unit(models);

        Assert.Equal(max, WargearConfig.MaxOf(unit, BigShoota));
        Assert.Equal(max, WargearConfig.CountOf(unit with { Wargear = WargearConfig.SetCount(unit, BigShoota, 99) }, BigShoota));
        Assert.Equal(0, WargearConfig.CountOf(unit with { Wargear = WargearConfig.SetCount(unit, BigShoota, -1) }, BigShoota));
    }

    [Fact]
    public void The_loadout_lists_the_chosen_weapons_and_the_pool_items()
    {
        var unit = Unit(20);
        unit = unit with { Wargear = WargearConfig.Choose(unit, Sergeant, Weapon, 1) };
        unit = unit with { Wargear = WargearConfig.SetCount(unit, BigShoota, 2) };

        Assert.Equal("Plasma pistol, 2× Big Shoota", WargearConfig.Loadout(unit));
    }

    [Fact]
    public void Items_with_a_listed_price_add_it_for_every_time_they_are_taken()
    {
        var costs = new[] { new WargearCost("Power fist", 10), new WargearCost("big shoota", 5) };
        var unit = Unit(20, costs);
        unit = unit with { Wargear = WargearConfig.Choose(unit, Sergeant, Weapon, 2) };
        unit = unit with { Wargear = WargearConfig.SetCount(unit, BigShoota, 2) };

        Assert.Equal(10 + 2 * 5, WargearConfig.Points(unit));
    }

    [Fact]
    public void Item_names_are_compared_ignoring_case_and_punctuation()
    {
        Assert.Equal(7, WargearConfig.PriceOf(Unit(costs: [new WargearCost("Twin Big-Shoota", 7)]).Datasheet, "twin big shoota"));
        Assert.Equal(0, WargearConfig.PriceOf(Unit().Datasheet, "Lascannon"));
    }

    [Fact]
    public void The_wargear_points_are_part_of_the_units_price_in_the_army()
    {
        var unit = Unit(10, [new WargearCost("Power fist", 10)]);
        var withFist = unit with { Wargear = WargearConfig.Choose(unit, Sergeant, Weapon, 2) };

        var priced = Assert.Single(ArmyPricing.Price([withFist]));

        Assert.Equal(100 + 10, priced.Points);
        Assert.Equal(10, priced.WargearPoints);
    }

    [Fact]
    public void Setting_the_wargear_of_a_unit_in_the_army_updates_only_that_unit_and_raises_changed()
    {
        var state = new ArmyBuilderState();
        state.SelectBattleSize(BattleSizes.StrikeForce);
        state.SelectFaction("orks");
        var datasheet = Unit().Datasheet;
        state.AddUnit(datasheet, new UnitSize(10, 100));
        state.AddUnit(datasheet, new UnitSize(10, 100));
        var changes = 0;
        state.Changed += () => changes++;

        state.SetWargear(state.Units[1].Id, WargearConfig.Choose(state.Units[1], Sergeant, Weapon, 1));

        Assert.Equal(0, WargearConfig.ChoiceOf(state.Units[0], Sergeant, Weapon));
        Assert.Equal(1, WargearConfig.ChoiceOf(state.Units[1], Sergeant, Weapon));
        Assert.Equal(1, changes);
    }
}
