using Servitor.App.ArmyBuilder.State;
using Servitor.App.ArmyCollection.Resolution;
using Servitor.App.ArmyCollection.State;
using Servitor.App.RulesData.Loading;
using Servitor.App.Tests.RulesData.Loading;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.ArmyCollection.Resolution;

public class SavedArmyResolverTests
{
    private sealed class FakeRulesDataProvider(MergedRulesData? data) : IMergedRulesDataProvider
    {
        public event Action? DataChanged
        {
            add { }
            remove { }
        }

        public Task<MergedRulesData?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(data);

        public Task StoreAsync(MergedRulesData value, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static readonly Detachment GreenTide = new("d1", "Green Tide", "", 1, MissionArchetype.TakeAndHold, 2, GameMode.Standard);
    private static readonly Enhancement Warboss = new("e1", "Da Biggest Boss", "", "", 20, "Green Tide", false, null);

    private static Datasheet Sheet(string name, int points, IReadOnlyList<UnitSize>? sizes = null, string factionSlug = "orks") => new(
        name, "Orks", points, new StatLine("6\"", "3", "5+", null, "1", "7+", "2"), [], [], [], [], [], [], null, [])
    {
        FactionSlugs = [factionSlug],
        Sizes = sizes ?? [],
    };

    private static MergedRulesData RulesData() => new(
        "v1",
        DateTimeOffset.UnixEpoch,
        [Sheet("Painboy", 45), Sheet("Boyz", 90, [new UnitSize(10, 90), new UnitSize(20, 180)])],
        [],
        [new MergedFactionRules("orks", "Orks", [], [], [Warboss], [GreenTide])],
        []);

    [Fact]
    public async Task Round_trips_a_captured_army_into_a_fresh_state()
    {
        var original = new ArmyBuilderState();
        original.SelectBattleSize(BattleSizes.StrikeForce);
        original.SelectFaction("orks");
        original.AddDetachment(GreenTide);

        original.AddUnit(Sheet("Painboy", 45), new UnitSize(0, 45));
        var leader = original.Units[0];
        original.SetWarlord(leader.Id);
        original.SetEnhancement(leader.Id, Warboss);

        original.AddUnit(Sheet("Boyz", 90, [new UnitSize(10, 90), new UnitSize(20, 180)]), new UnitSize(20, 180));
        var led = original.Units[1];
        original.SetLeader(led.Id, leader.Id);

        var saved = ArmySnapshot.Capture("Test Army", original);
        var reopened = new ArmyBuilderState();

        var result = await SavedArmyResolver.ResolveAsync(
            saved, reopened, new FakeRulesDataProvider(RulesData()), new FakeDatasheetCatalog(RulesData().Datasheets));

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.Equal("orks", reopened.Setup.FactionId);
        Assert.Equal(BattleSizes.StrikeForce, reopened.Setup.BattleSize);
        Assert.Equal(2, reopened.Units.Count);

        var reopenedLeader = Assert.Single(reopened.Units, u => u.Datasheet.Name == "Painboy");
        Assert.True(reopenedLeader.IsWarlord);
        Assert.Equal("Da Biggest Boss", reopenedLeader.Enhancement?.Name);

        var reopenedLed = Assert.Single(reopened.Units, u => u.Datasheet.Name == "Boyz");
        Assert.Equal(reopenedLeader.Id, reopenedLed.LeaderUnitId);
        Assert.Equal(20, reopenedLed.Size.Models);
    }

    [Fact]
    public async Task An_unknown_datasheet_fails_without_mutating_state()
    {
        var saved = new SavedArmy(
            Guid.NewGuid(), "Test", DateTimeOffset.UtcNow, "orks", "Strike Force", [], ["Green Tide"],
            [new SavedUnit(Guid.NewGuid(), "Not A Real Unit", 1, WargearSelection.Default, null, null, false, null)]);

        var state = new ArmyBuilderState();
        var result = await SavedArmyResolver.ResolveAsync(
            saved, state, new FakeRulesDataProvider(RulesData()), new FakeDatasheetCatalog(RulesData().Datasheets));

        Assert.False(result.IsSuccess);
        Assert.Empty(state.Units);
        Assert.Null(state.Setup.FactionId);
    }

    [Fact]
    public async Task No_rules_data_yet_is_a_handled_failure()
    {
        var saved = new SavedArmy(Guid.NewGuid(), "Test", DateTimeOffset.UtcNow, "orks", "Strike Force", [], [], []);
        var state = new ArmyBuilderState();

        var result = await SavedArmyResolver.ResolveAsync(
            saved, state, new FakeRulesDataProvider(null), new FakeDatasheetCatalog([]));

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task ValidateAsync_reports_the_same_errors_without_touching_state()
    {
        var saved = new SavedArmy(
            Guid.NewGuid(), "Test", DateTimeOffset.UtcNow, "orks", "Strike Force", [], ["Unknown Detachment"], []);

        var errors = await SavedArmyResolver.ValidateAsync(
            saved, new FakeRulesDataProvider(RulesData()), new FakeDatasheetCatalog(RulesData().Datasheets));

        Assert.Contains(errors, e => e.Contains("Unknown detachment"));
    }

    [Fact]
    public async Task ValidateAsync_finds_nothing_wrong_with_a_valid_saved_army()
    {
        var saved = new SavedArmy(
            Guid.NewGuid(), "Test", DateTimeOffset.UtcNow, "orks", "Strike Force", [], ["Green Tide"],
            [new SavedUnit(Guid.NewGuid(), "Painboy", 0, WargearSelection.Default, null, null, false, null)]);

        var errors = await SavedArmyResolver.ValidateAsync(
            saved, new FakeRulesDataProvider(RulesData()), new FakeDatasheetCatalog(RulesData().Datasheets));

        Assert.Empty(errors);
    }
}
