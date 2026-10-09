using Servitor.App.ArmyBuilder.State;
using Servitor.App.BattleTracker.ActionQueue;
using Servitor.App.BattleTracker.State;
using Servitor.App.Scoring.Tracking;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.BattleTracker.ActionQueue;

public class ActionQueueBuilderTests
{
    private static readonly ArmySetup Army = BattleTrackerFixtures.OrkArmy().Setup;

    private static string[] CoreNames(BattlePhase phase, Side active, Side first = Side.You) =>
        Names(Queue(phase, active, first).CoreStratagems);

    private static ActionQueueContent Queue(BattlePhase phase, Side active, Side first = Side.You) =>
        ActionQueueBuilder.Build(BattleTrackerFixtures.Data(), Army, new TurnContext(phase, active, first));

    private static string[] Names(IEnumerable<Stratagem> stratagems) => stratagems.Select(s => s.Name).ToArray();

    [Fact]
    public void A_your_turn_stratagem_only_shows_in_your_own_turn()
    {
        Assert.Contains("GRENADE", CoreNames(BattlePhase.Shooting, Side.You));
        Assert.DoesNotContain("GRENADE", CoreNames(BattlePhase.Shooting, Side.Enemy));
    }

    [Fact]
    public void An_enemy_turn_stratagem_only_shows_in_the_enemy_turn()
    {
        Assert.Contains("SMOKESCREEN", CoreNames(BattlePhase.Shooting, Side.Enemy));
        Assert.DoesNotContain("SMOKESCREEN", CoreNames(BattlePhase.Shooting, Side.You));
    }

    [Fact]
    public void An_either_turn_stratagem_shows_in_both_turns_but_only_in_its_phase()
    {
        Assert.Contains("ANY FIGHT", CoreNames(BattlePhase.Fight, Side.You));
        Assert.Contains("ANY FIGHT", CoreNames(BattlePhase.Fight, Side.Enemy));
        Assert.DoesNotContain("ANY FIGHT", CoreNames(BattlePhase.Shooting, Side.You));
    }

    [Fact]
    public void An_any_phase_stratagem_shows_in_every_phase_of_both_turns()
    {
        foreach (var side in new[] { Side.You, Side.Enemy })
        {
            foreach (var phase in Enum.GetValues<BattlePhase>())
            {
                Assert.Contains("COMMAND RE-ROLL", CoreNames(phase, side));
            }
        }
    }

    [Fact]
    public void Round_start_shows_in_the_command_phase_of_the_first_turn_only()
    {
        Assert.Contains("ROUND OPENER", CoreNames(BattlePhase.Command, Side.You, first: Side.You));
        Assert.DoesNotContain("ROUND OPENER", CoreNames(BattlePhase.Command, Side.Enemy, first: Side.You));
        Assert.Contains("ROUND OPENER", CoreNames(BattlePhase.Command, Side.Enemy, first: Side.Enemy));
        Assert.DoesNotContain("ROUND OPENER", CoreNames(BattlePhase.Movement, Side.You));
    }

    [Fact]
    public void Round_end_shows_in_the_last_phase_of_the_second_turn_only()
    {
        Assert.Contains("ROUND CLOSER", CoreNames(BattlePhase.EndOfTurn, Side.Enemy, first: Side.You));
        Assert.DoesNotContain("ROUND CLOSER", CoreNames(BattlePhase.EndOfTurn, Side.You, first: Side.You));
        Assert.DoesNotContain("ROUND CLOSER", CoreNames(BattlePhase.Fight, Side.Enemy, first: Side.You));
    }

    [Fact]
    public void Start_of_your_turn_shows_in_your_command_phase_and_end_of_enemy_turn_in_the_enemy_end_of_turn_phase()
    {
        Assert.Contains("MY TURN OPENER", CoreNames(BattlePhase.Command, Side.You));
        Assert.DoesNotContain("MY TURN OPENER", CoreNames(BattlePhase.Command, Side.Enemy));
        Assert.DoesNotContain("MY TURN OPENER", CoreNames(BattlePhase.Movement, Side.You));

        Assert.Contains("MOVE OUT", CoreNames(BattlePhase.EndOfTurn, Side.Enemy));
        Assert.DoesNotContain("MOVE OUT", CoreNames(BattlePhase.EndOfTurn, Side.You));
        Assert.DoesNotContain("MOVE OUT", CoreNames(BattlePhase.Command, Side.Enemy));
    }

    [Fact]
    public void Deployment_shows_only_what_has_no_turn_or_moment_tie()
    {
        Assert.Equal(["COMMAND RE-ROLL"], CoreNames(BattlePhase.Deployment, Side.You));
    }

    [Fact]
    public void Detachment_stratagems_follow_phase_and_turn_too()
    {
        Assert.Equal(["BIG KRUNCH"], Names(Queue(BattlePhase.Shooting, Side.You).DetachmentStratagems));
        Assert.Equal(["BIG KRUNCH"], Names(Queue(BattlePhase.Charge, Side.You).DetachmentStratagems));
        Assert.Empty(Queue(BattlePhase.Shooting, Side.Enemy).DetachmentStratagems);
        Assert.Empty(Queue(BattlePhase.Fight, Side.You).DetachmentStratagems);
    }

    [Fact]
    public void Only_the_stratagems_of_the_armys_detachments_are_listed()
    {
        Assert.DoesNotContain("OTHER DETACHMENT", Names(Queue(BattlePhase.Shooting, Side.You).DetachmentStratagems));
    }

    [Fact]
    public void Non_standard_game_modes_are_left_out()
    {
        Assert.DoesNotContain("BOARDING ONLY", CoreNames(BattlePhase.Shooting, Side.You));
    }

    [Fact]
    public void Without_an_army_only_the_core_stratagems_are_listed()
    {
        var queue = ActionQueueBuilder.Build(BattleTrackerFixtures.Data(), ArmySetup.Empty, new TurnContext(BattlePhase.Shooting, Side.You, Side.You));

        Assert.NotEmpty(queue.CoreStratagems);
        Assert.Empty(queue.DetachmentStratagems);
    }

    [Fact]
    public void Without_rules_data_the_queue_is_empty()
    {
        Assert.Same(ActionQueueContent.Empty, ActionQueueBuilder.Build(null, Army, new TurnContext(BattlePhase.Shooting, Side.You, Side.You)));
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

        Assert.Equal(expectedCount, StratagemTiming.Of(stratagem).Phases.Count);
    }

    [Theory]
    [InlineData("Your turn", TurnScope.Your)]
    [InlineData("Opponent’s turn", TurnScope.Opponent)]
    [InlineData("Either player’s turn", TurnScope.Either)]
    public void Turn_text_is_read_into_a_scope(string text, TurnScope expected)
    {
        Assert.Equal(expected, StratagemTiming.ScopeOf(BattleTrackerFixtures.Stratagem("x", "X", "Any phase", text)));
    }

    [Theory]
    [InlineData("Start of the battle round.", TimingMoment.RoundStart)]
    [InlineData("Start of the first battle round, before anything.", TimingMoment.RoundStart)]
    [InlineData("End of the battle round.", TimingMoment.RoundEnd)]
    [InlineData("Start of your turn.", TimingMoment.TurnStart)]
    [InlineData("End of your opponent’s turn.", TimingMoment.TurnEnd)]
    [InlineData("End of the turn.", TimingMoment.TurnEnd)]
    [InlineData("Start or end of any turn, when an enemy unit attempts to Seize.", TimingMoment.TurnStartOrEnd)]
    [InlineData("Your Command phase, until the start of your next turn.", TimingMoment.None)]
    public void A_when_text_that_opens_with_a_moment_ties_an_any_phase_stratagem_to_it(string when, TimingMoment expected)
    {
        var stratagem = BattleTrackerFixtures.Stratagem("x", "X", "Any phase", when: when);

        Assert.Equal(expected, StratagemTiming.Of(stratagem).Moment);
    }

    [Fact]
    public void A_moment_in_the_when_text_is_ignored_when_the_stratagem_names_a_phase()
    {
        var stratagem = BattleTrackerFixtures.Stratagem("x", "X", "Shooting phase", when: "Start of your turn.");

        Assert.Equal(TimingMoment.None, StratagemTiming.Of(stratagem).Moment);
    }

    [Theory]
    [InlineData("Shooting phase", "Your turn", "Your Shooting phase")]
    [InlineData("Shooting phase", "Opponent’s turn", "Enemy Shooting phase")]
    [InlineData("Shooting phase", "Either player’s turn", "Any Shooting phase")]
    [InlineData("Shooting or Fight phase", "Opponent’s turn", "Enemy Shooting or Fight phase")]
    [InlineData("Any phase", "Either player’s turn", "Any phase")]
    [InlineData("Any phase", "Your turn", "Your turn, any phase")]
    public void The_timing_label_names_whose_turn_and_which_phases(string phase, string turn, string expected)
    {
        var timing = StratagemTiming.Of(BattleTrackerFixtures.Stratagem("x", "X", phase, turn));

        Assert.Equal(expected, StratagemLabels.Timing(timing));
    }

    [Theory]
    [InlineData("Start of the battle round.", "Either player’s turn", "Start of battle round")]
    [InlineData("End of your opponent’s turn.", "Opponent’s turn", "End of enemy turn")]
    [InlineData("Start of your turn.", "Your turn", "Start of your turn")]
    public void The_timing_label_names_the_moment(string when, string turn, string expected)
    {
        var timing = StratagemTiming.Of(BattleTrackerFixtures.Stratagem("x", "X", "Any phase", turn, when: when));

        Assert.Equal(expected, StratagemLabels.Timing(timing));
    }
}
