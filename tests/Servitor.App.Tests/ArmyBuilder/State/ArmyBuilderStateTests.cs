using Servitor.App.ArmyBuilder.State;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.ArmyBuilder.State;

public class ArmyBuilderStateTests
{
    private static Detachment Det(string id, int dp, GameMode mode = GameMode.Standard) =>
        new(id, id, "", dp, MissionArchetype.TakeAndHold, 4, mode);

    private static ArmyBuilderState Ready(BattleSize size)
    {
        var state = new ArmyBuilderState();
        state.SelectBattleSize(size);
        state.SelectFaction("orks");
        return state;
    }

    [Fact]
    public void Detachments_cannot_be_added_before_battle_size_and_faction_are_chosen()
    {
        var state = new ArmyBuilderState();
        Assert.Equal(DetachmentAddResult.NoBattleSize, state.AddDetachment(Det("a", 1)));

        state.SelectBattleSize(BattleSizes.StrikeForce);
        Assert.Equal(DetachmentAddResult.NoFaction, state.AddDetachment(Det("a", 1)));
        Assert.Empty(state.Setup.Detachments);
    }

    [Fact]
    public void Detachments_are_added_up_to_the_dp_cap_of_the_battle_size()
    {
        var state = Ready(BattleSizes.Incursion);

        Assert.Equal(DetachmentAddResult.Added, state.AddDetachment(Det("a", 1)));
        Assert.Equal(DetachmentAddResult.Added, state.AddDetachment(Det("b", 1)));
        Assert.Equal(DetachmentAddResult.ExceedsDetachmentPoints, state.AddDetachment(Det("c", 1)));
        Assert.Equal(2, state.Setup.DetachmentPointsUsed);
    }

    [Fact]
    public void Each_battle_size_enforces_its_own_dp_cap()
    {
        Assert.Equal(DetachmentAddResult.ExceedsDetachmentPoints, Ready(BattleSizes.Incursion).AddDetachment(Det("a", 4)));
        Assert.Equal(DetachmentAddResult.Added, Ready(BattleSizes.StrikeForce).AddDetachment(Det("a", 3)));
        Assert.Equal(DetachmentAddResult.ExceedsDetachmentPoints, Ready(BattleSizes.StrikeForce).AddDetachment(Det("a", 4)));
        Assert.Equal(DetachmentAddResult.Added, Ready(BattleSizes.Onslaught).AddDetachment(Det("a", 4)));
    }

    [Fact]
    public void An_incursion_army_may_take_a_single_three_dp_detachment_but_only_as_its_only_one()
    {
        var alone = Ready(BattleSizes.Incursion);
        Assert.Equal(DetachmentAddResult.Added, alone.AddDetachment(Det("big", 3)));
        Assert.Equal(DetachmentAddResult.ExceedsDetachmentPoints, alone.AddDetachment(Det("small", 1)));

        var second = Ready(BattleSizes.Incursion);
        second.AddDetachment(Det("a", 1));
        Assert.Equal(DetachmentAddResult.ExceedsDetachmentPoints, second.AddDetachment(Det("big", 3)));
    }

    [Fact]
    public void A_detachment_cannot_be_selected_twice()
    {
        var state = Ready(BattleSizes.Onslaught);
        state.AddDetachment(Det("a", 1));

        Assert.Equal(DetachmentAddResult.AlreadySelected, state.AddDetachment(Det("a", 1)));
    }

    [Fact]
    public void Boarding_actions_detachments_are_rejected()
    {
        var state = Ready(BattleSizes.Onslaught);

        Assert.Equal(DetachmentAddResult.NotStandardGame, state.AddDetachment(Det("a", 1, GameMode.BoardingActions)));
    }

    [Fact]
    public void Removing_a_detachment_frees_its_dp()
    {
        var state = Ready(BattleSizes.Incursion);
        state.AddDetachment(Det("a", 2));

        state.RemoveDetachment("a");

        Assert.Equal(0, state.Setup.DetachmentPointsUsed);
        Assert.Equal(DetachmentAddResult.Added, state.AddDetachment(Det("b", 2)));
    }

    [Fact]
    public void Changing_the_faction_discards_the_selected_detachments()
    {
        var state = Ready(BattleSizes.StrikeForce);
        state.AddDetachment(Det("a", 1));

        state.SelectFaction("orks");
        Assert.Single(state.Setup.Detachments);

        state.SelectFaction("necrons");
        Assert.Empty(state.Setup.Detachments);
    }

    [Fact]
    public void Skipping_and_resuming_the_setup_is_tracked_and_raises_changed()
    {
        var state = new ArmyBuilderState();
        var changes = 0;
        state.Changed += () => changes++;

        state.SkipSetup();
        Assert.True(state.Setup.IsSkipped);

        state.ResumeSetup();
        Assert.False(state.Setup.IsSkipped);
        Assert.Equal(2, changes);
    }
}
