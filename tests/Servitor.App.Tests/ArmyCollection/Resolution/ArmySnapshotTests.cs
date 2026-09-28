using Servitor.App.ArmyBuilder.State;
using Servitor.App.ArmyCollection.Resolution;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.ArmyCollection.Resolution;

public class ArmySnapshotTests
{
    private static Datasheet Sheet(string name, IReadOnlyList<UnitSize>? sizes = null) => new(
        name, "Orks", 45, new StatLine("6\"", "3", "5+", null, "1", "7+", "2"), [], [], [], [], [], [], null, [])
    {
        FactionSlugs = ["orks"],
        Sizes = sizes ?? [],
    };

    [Fact]
    public void Captures_setup_and_units_by_name()
    {
        var state = new ArmyBuilderState();
        state.SelectBattleSize(BattleSizes.StrikeForce);
        state.SelectFaction("orks", ["imperial-knights"]);
        var detachment = new Detachment("d1", "Green Tide", "", 1, MissionArchetype.TakeAndHold, 2, GameMode.Standard);
        state.AddDetachment(detachment);

        state.AddUnit(Sheet("Painboy"), new UnitSize(0, 45));
        var leader = state.Units[0];
        state.SetWarlord(leader.Id);

        state.AddUnit(Sheet("Boyz", [new UnitSize(10, 90), new UnitSize(20, 180)]), new UnitSize(20, 180));
        var led = state.Units[1];
        state.SetLeader(led.Id, leader.Id);

        var saved = ArmySnapshot.Capture("My Waaagh", state);

        Assert.Equal("My Waaagh", saved.Name);
        Assert.Equal("orks", saved.FactionId);
        Assert.Equal("Strike Force", saved.BattleSizeName);
        Assert.Equal(["imperial-knights"], saved.AllyFactionIds);
        Assert.Equal(["Green Tide"], saved.DetachmentNames);
        Assert.Equal(2, saved.Units.Count);

        var savedLeader = Assert.Single(saved.Units, u => u.DatasheetName == "Painboy");
        Assert.True(savedLeader.IsWarlord);

        var savedLed = Assert.Single(saved.Units, u => u.DatasheetName == "Boyz");
        Assert.Equal(savedLeader.Id, savedLed.LeaderUnitId);
        Assert.Equal(20, savedLed.ModelCount);
    }

    [Fact]
    public void Throws_when_faction_or_battle_size_not_yet_chosen()
    {
        var state = new ArmyBuilderState();

        Assert.Throws<InvalidOperationException>(() => ArmySnapshot.Capture("x", state));
    }
}
