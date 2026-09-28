using Servitor.App.ArmyBuilder.State;
using Servitor.App.ArmyBuilder.Validation;
using Servitor.App.Tests.ArmyBuilder.Catalog;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.ArmyBuilder.State;

public class AlliesStateTests
{
    private static readonly UnitSize Size = new(1, 100);
    private static readonly Datasheet Agent = Units.Sheet("Inquisitor", factions: ["imperial-agents"]);
    private static readonly Datasheet Marine = Units.Sheet("Captain", factions: ["space-marines"]);

    private static ArmyBuilderState Army(params string[] allies)
    {
        var state = new ArmyBuilderState();
        state.SelectBattleSize(BattleSizes.StrikeForce);
        state.SelectFaction("space-marines", allies);
        return state;
    }

    [Fact]
    public void The_setup_knows_the_factions_the_army_may_ally_with()
    {
        Assert.Equal(["imperial-agents", "imperial-knights"], Army("imperial-agents", "imperial-knights").Setup.AllyFactionIds);
        Assert.Empty(Army().Setup.AllyFactionIds);
    }

    [Fact]
    public void A_unit_of_an_allowed_ally_faction_is_added_and_flagged_as_allied()
    {
        var state = Army("imperial-agents");

        Assert.Equal(AddUnitResult.Added, state.AddUnit(Agent, Size, "imperial-agents"));

        Assert.Equal("imperial-agents", Assert.Single(state.Units).AlliedFaction);
    }

    [Fact]
    public void Own_units_are_not_flagged_as_allied()
    {
        var state = Army("imperial-agents");

        state.AddUnit(Marine, Size);

        Assert.Null(state.Units[0].AlliedFaction);
    }

    [Fact]
    public void A_unit_from_a_faction_the_army_may_not_ally_with_is_rejected()
    {
        var state = Army("imperial-knights");

        Assert.Equal(AddUnitResult.AllyNotAllowed, state.AddUnit(Agent, Size, "imperial-agents"));
        Assert.Empty(state.Units);
    }

    [Fact]
    public void A_unit_that_does_not_belong_to_the_named_ally_faction_is_rejected()
    {
        var state = Army("imperial-agents");

        Assert.Equal(AddUnitResult.AllyNotAllowed, state.AddUnit(Marine, Size, "imperial-agents"));
    }

    [Fact]
    public void A_unit_of_an_ally_faction_cannot_be_added_as_an_own_unit()
    {
        var state = Army("imperial-agents");

        Assert.Equal(AddUnitResult.NotInFaction, state.AddUnit(Agent, Size));
    }

    [Fact]
    public void The_same_datasheet_limit_counts_allied_and_own_copies_together()
    {
        var state = Army("imperial-agents");
        var shared = Units.Sheet("Ministorum Priest", factions: ["space-marines", "imperial-agents"], constraint: new CompositionConstraint(1, 1));

        Assert.Equal(AddUnitResult.Added, state.AddUnit(shared, Size));
        Assert.Equal(AddUnitResult.AtLimit, state.AddUnit(shared, Size, "imperial-agents"));
    }

    [Fact]
    public void Choosing_the_same_faction_again_updates_the_allies_but_keeps_the_units()
    {
        var state = Army();
        state.AddUnit(Marine, Size);

        state.SelectFaction("space-marines", ["imperial-agents"]);

        Assert.Equal(["imperial-agents"], state.Setup.AllyFactionIds);
        Assert.Single(state.Units);
    }

    [Fact]
    public void Changing_the_faction_discards_the_units_and_takes_over_the_new_factions_allies()
    {
        var state = Army("imperial-agents");
        state.AddUnit(Agent, Size, "imperial-agents");

        state.SelectFaction("orks", []);

        Assert.Empty(state.Units);
        Assert.Empty(state.Setup.AllyFactionIds);
    }
}

public class AllyRuleTests
{
    private static ArmyUnit Allied(string faction, string unitFaction) =>
        new(Guid.NewGuid(), Units.Sheet("Inquisitor", factions: [unitFaction]), new UnitSize(1, 100)) { AlliedFaction = faction };

    private static ArmySetup Setup(params string[] allies) =>
        new(BattleSizes.StrikeForce, "space-marines", []) { AllyFactionIds = allies };

    [Fact]
    public void An_allied_unit_from_a_faction_the_army_may_not_ally_with_is_reported()
    {
        var issue = Assert.Single(new AllyRule().Check(ArmyContext.Create(Setup("imperial-knights"), [Allied("imperial-agents", "imperial-agents")])));

        Assert.Equal(ArmyIssueKind.AllyNotAllowed, issue.Kind);
        Assert.Equal("Inquisitor is taken as an ally from imperial-agents, which the army faction may not ally with.", issue.Message);
    }

    [Fact]
    public void An_allied_unit_that_does_not_belong_to_its_ally_faction_is_reported()
    {
        Assert.Single(new AllyRule().Check(ArmyContext.Create(Setup("imperial-agents"), [Allied("imperial-agents", "orks")])));
    }

    [Fact]
    public void Allowed_allied_units_and_own_units_are_fine()
    {
        var own = new ArmyUnit(Guid.NewGuid(), Units.Sheet("Captain"), new UnitSize(1, 100));

        Assert.Empty(new AllyRule().Check(ArmyContext.Create(Setup("imperial-agents"), [Allied("imperial-agents", "imperial-agents"), own])));
    }
}
