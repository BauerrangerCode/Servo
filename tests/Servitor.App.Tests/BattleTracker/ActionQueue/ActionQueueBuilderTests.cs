using Servitor.App.ArmyBuilder.State;
using Servitor.App.BattleTracker.ActionQueue;
using Servitor.App.BattleTracker.State;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.BattleTracker.ActionQueue;

public class ActionQueueBuilderTests
{
    private static readonly ArmySetup Army = BattleTrackerFixtures.OrkArmy().Setup;

    private static string[] Names(IEnumerable<Stratagem> stratagems) => stratagems.Select(s => s.Name).ToArray();

    [Fact]
    public void A_phase_lists_only_the_stratagems_of_that_phase_and_those_usable_in_any_phase()
    {
        var shooting = ActionQueueBuilder.Build(BattleTrackerFixtures.Data(), Army, BattlePhase.Shooting);

        Assert.Equal(["COMMAND RE-ROLL", "GRENADE"], Names(shooting.CoreStratagems));
        Assert.Equal(["BIG KRUNCH"], Names(shooting.DetachmentStratagems));
    }

    [Fact]
    public void A_stratagem_for_two_phases_shows_in_both_and_in_no_other()
    {
        var data = BattleTrackerFixtures.Data();

        Assert.Contains("BIG KRUNCH", Names(ActionQueueBuilder.Build(data, Army, BattlePhase.Charge).DetachmentStratagems));
        Assert.Empty(ActionQueueBuilder.Build(data, Army, BattlePhase.Fight).DetachmentStratagems);
    }

    [Fact]
    public void Both_turn_scopes_show_together_because_the_scope_is_only_a_tag()
    {
        var movement = ActionQueueBuilder.Build(BattleTrackerFixtures.Data(), Army, BattlePhase.Movement);

        Assert.Equal(["COMMAND RE-ROLL", "FIRE OVERWATCH"], Names(movement.CoreStratagems));
    }

    [Fact]
    public void Only_the_stratagems_of_the_armys_detachments_are_listed()
    {
        var queue = ActionQueueBuilder.Build(BattleTrackerFixtures.Data(), Army, BattlePhase.Shooting);

        Assert.DoesNotContain("OTHER DETACHMENT", Names(queue.DetachmentStratagems));
    }

    [Fact]
    public void Non_standard_game_modes_are_left_out()
    {
        var queue = ActionQueueBuilder.Build(BattleTrackerFixtures.Data(), Army, BattlePhase.Shooting);

        Assert.DoesNotContain("BOARDING ONLY", Names(queue.CoreStratagems));
    }

    [Fact]
    public void Without_an_army_only_the_core_stratagems_are_listed()
    {
        var queue = ActionQueueBuilder.Build(BattleTrackerFixtures.Data(), ArmySetup.Empty, BattlePhase.Shooting);

        Assert.NotEmpty(queue.CoreStratagems);
        Assert.Empty(queue.DetachmentStratagems);
    }

    [Fact]
    public void Without_rules_data_the_queue_is_empty()
    {
        Assert.Same(ActionQueueContent.Empty, ActionQueueBuilder.Build(null, Army, BattlePhase.Shooting));
    }

    [Theory]
    [InlineData("Any phase", 7)]
    [InlineData("Shooting phase", 1)]
    [InlineData("Shooting or Fight phase", 2)]
    [InlineData("Command or Fight phase", 2)]
    [InlineData("Something unexpected", 7)]
    public void Phase_text_is_read_into_phases_and_unknown_text_means_every_phase(string text, int expectedCount)
    {
        var stratagem = BattleTrackerFixtures.Stratagem("x", "X", text);

        Assert.Equal(expectedCount, StratagemTiming.PhasesOf(stratagem).Count);
    }

    [Theory]
    [InlineData("Your turn", TurnScope.Your)]
    [InlineData("Opponent’s turn", TurnScope.Opponent)]
    [InlineData("Either player’s turn", TurnScope.Either)]
    public void Turn_text_is_read_into_a_scope(string text, TurnScope expected)
    {
        Assert.Equal(expected, StratagemTiming.ScopeOf(BattleTrackerFixtures.Stratagem("x", "X", "Any phase", text)));
    }
}
