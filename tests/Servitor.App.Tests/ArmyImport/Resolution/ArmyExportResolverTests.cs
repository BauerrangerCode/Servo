using System.Text.Json;
using Servitor.App.ArmyBuilder.State;
using Servitor.App.ArmyImport.Parsing;
using Servitor.App.ArmyImport.Resolution;
using Servitor.App.RulesData.Loading;
using Servitor.App.Tests.RulesData.Loading;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.ArmyImport.Resolution;

public class ArmyExportResolverTests
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

    internal static MergedRulesData LoadRealMergedData()
    {
        var json = File.ReadAllText(TestPaths.WwwrootDataFile("merged", "rules-data.json"));
        return JsonSerializer.Deserialize<MergedRulesData>(json, MergedDataJson.Options)
            ?? throw new InvalidOperationException("Failed to deserialize the merged rules data fixture.");
    }

    private static async Task<(ImportResolveResult Result, ArmyBuilderState State)> ResolveAsync(string fileName)
    {
        var rulesData = LoadRealMergedData();
        var parseResult = WarscribeExportParser.Parse(File.ReadAllText(TestPaths.DataFile(fileName)));
        Assert.True(parseResult.IsSuccess, string.Join("; ", parseResult.Errors));

        var state = new ArmyBuilderState();
        var result = await ArmyExportResolver.ResolveAsync(
            parseResult.Export!, state, new FakeRulesDataProvider(rulesData), new FakeDatasheetCatalog(rulesData.Datasheets));
        return (result, state);
    }

    [Fact]
    public async Task Resolves_the_three_detachment_Ork_sample_into_a_full_army()
    {
        var (result, state) = await ResolveAsync("Army1.txt");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.Equal("orks", state.Setup.FactionId);
        Assert.Equal(3, state.Setup.Detachments.Count);
        Assert.Equal(16, state.Units.Count);
    }

    [Fact]
    public async Task Sets_the_warlord_flag_on_the_correct_unit()
    {
        var (result, state) = await ResolveAsync("Army1.txt");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        var warlord = Assert.Single(state.Units, u => u.IsWarlord);
        Assert.Equal("Ghazghkull Thraka", warlord.Datasheet.Name);
    }

    [Fact]
    public async Task Attaches_led_units_to_their_leader()
    {
        var (result, state) = await ResolveAsync("Army1.txt");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        var painboy = Assert.Single(state.Units, u => u.Datasheet.Name == "Painboy");
        var ledUnit = Assert.Single(state.Units, u => u.LeaderUnitId == painboy.Id);
        Assert.Equal("Boyz", ledUnit.Datasheet.Name);
    }

    [Fact]
    public async Task Resolves_enhancements_onto_the_correct_unit()
    {
        var (result, state) = await ResolveAsync("Army1.txt");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));

        // "Throat-slittas (Upgrade)" is taken twice in Army1.txt (Kommandos and Stormboyz) — valid, since
        // Upgrade-tagged enhancements allow multiple copies (see docs/Backlog.md E3.4 decision D11).
        var kommandos = Assert.Single(state.Units, u => u.Datasheet.Name == "Kommandos" && u.Enhancement is not null);
        Assert.Equal("Throat-slittas", kommandos.Enhancement!.Name);
        Assert.True(kommandos.Enhancement.IsUpgrade);

        var stormboyz = Assert.Single(state.Units, u => u.Datasheet.Name == "Stormboyz" && u.Enhancement is not null);
        Assert.Equal("Throat-slittas", stormboyz.Enhancement!.Name);
    }

    [Fact]
    public async Task Resolves_the_space_wolves_sample_with_no_enhancements_or_leaders()
    {
        var (result, state) = await ResolveAsync("Army3.txt");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.Equal(12, state.Units.Count);
        Assert.All(state.Units, u => Assert.Null(u.LeaderUnitId));
    }

    [Fact]
    public async Task An_unknown_unit_name_fails_without_mutating_state()
    {
        var rulesData = LoadRealMergedData();
        var text = File.ReadAllText(TestPaths.DataFile("Army1.txt")).Replace("20x Boyz", "20x Not A Real Unit");
        var parseResult = WarscribeExportParser.Parse(text);
        Assert.True(parseResult.IsSuccess, string.Join("; ", parseResult.Errors));

        var state = new ArmyBuilderState();
        var result = await ArmyExportResolver.ResolveAsync(
            parseResult.Export!, state, new FakeRulesDataProvider(rulesData), new FakeDatasheetCatalog(rulesData.Datasheets));

        Assert.False(result.IsSuccess);
        Assert.Empty(state.Units);
        Assert.Null(state.Setup.FactionId);
    }

    [Fact]
    public async Task No_rules_data_yet_is_a_handled_failure()
    {
        var parseResult = WarscribeExportParser.Parse(File.ReadAllText(TestPaths.DataFile("Army1.txt")));
        Assert.True(parseResult.IsSuccess);

        var state = new ArmyBuilderState();
        var result = await ArmyExportResolver.ResolveAsync(
            parseResult.Export!, state, new FakeRulesDataProvider(null), new FakeDatasheetCatalog([]));

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task Importing_twice_into_the_same_shared_state_succeeds_both_times()
    {
        // IArmyBuilderState is shared across Import/Build/Play — importing the same (or another) list a second
        // time into a state that already holds a resolved army must not fail with AlreadySelected/AtLimit against
        // what the first import already put there.
        var rulesData = LoadRealMergedData();
        var parseResult = WarscribeExportParser.Parse(File.ReadAllText(TestPaths.DataFile("Army1.txt")));
        Assert.True(parseResult.IsSuccess, string.Join("; ", parseResult.Errors));

        var state = new ArmyBuilderState();
        var first = await ArmyExportResolver.ResolveAsync(
            parseResult.Export!, state, new FakeRulesDataProvider(rulesData), new FakeDatasheetCatalog(rulesData.Datasheets));
        var second = await ArmyExportResolver.ResolveAsync(
            parseResult.Export!, state, new FakeRulesDataProvider(rulesData), new FakeDatasheetCatalog(rulesData.Datasheets));

        Assert.True(first.IsSuccess, string.Join("; ", first.Errors));
        Assert.True(second.IsSuccess, string.Join("; ", second.Errors));
        Assert.Equal(16, state.Units.Count);
        Assert.Equal(3, state.Setup.Detachments.Count);
    }
}
