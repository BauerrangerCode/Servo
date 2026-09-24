using System.Text.Json;
using Bunit;
using Servitor.App.RulesData.Storage;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.RulesData.Storage;

public class IndexedDbRulesDataStoreTests : BunitContext
{
    private const string StoreKey = "merged-rules-data/data";

    private static MergedRulesData CreateSampleData() => new(
        DataVersion: "8f5b01a+2026-09-11",
        GeneratedAtUtc: new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero),
        Datasheets:
        [
            new Datasheet(
                "Cadian Shock Troops",
                "Astra Militarum",
                70,
                new StatLine("6\"", "3", "5+", null, "1", "7+", "2"),
                [new WeaponProfile("Lasgun", WeaponType.Ranged, "24\"", "1", "4+", "3", "0", "1", ["Rapid Fire 1"])],
                [],
                [new Ability("Shock Troops", "Holds objectives.", AbilityCategory.Named)],
                [],
                [],
                ["Infantry", "Battleline"],
                new CompositionConstraint(6, 4),
                ["s1"]),
        ],
        CoreStratagems: [],
        Factions:
        [
            new MergedFactionRules(
                "astra-militarum",
                "Astra Militarum",
                [new Stratagem("s1", "Command Re-roll", StratagemCategory.Other, 1, "", "Any", "Any Phase", "Any phase.", "One unit.", "Re-roll a roll.", "", "", "")],
                [new DetachmentAbility("a1", "Steel Discipline", "", "Some passive rule.", "Combined Regiment")],
                [new Enhancement("e1", "Kurov's Aquila", "", "Ignore the first mortal wound.", 30, "Combined Regiment", false, null)]),
        ],
        CoreRules: [new CoreRule("r1", "Deep Strike", null, "Set up **anywhere**.", CoreRuleSource.BsData)]);

    [Fact]
    public async Task Saved_data_round_trips_through_the_store_unchanged()
    {
        var module = JSInterop.SetupModule("./js/indexedDbInterop.js");
        module.SetupVoid("putDocument", _ => true).SetVoidResult();

        var original = CreateSampleData();
        var store = new IndexedDbRulesDataStore(JSInterop.JSRuntime);

        await store.SaveAsync(StoreKey, original);

        var savedInvocation = Assert.Single(module.Invocations, i => i.Identifier == "putDocument");
        Assert.Equal(StoreKey, savedInvocation.Arguments[0]);
        var savedJson = Assert.IsType<string>(savedInvocation.Arguments[1]);

        module.Setup<string?>("getDocument", _ => true).SetResult(savedJson);

        var reloaded = await store.LoadAsync<MergedRulesData>(StoreKey);

        // Record-generated equality compares IReadOnlyList<T> properties by reference, which two independently
        // deserialized object graphs never share — compare content via their canonical JSON form instead.
        Assert.NotNull(reloaded);
        Assert.Equal(
            JsonSerializer.Serialize(original, MergedDataJson.Options),
            JsonSerializer.Serialize(reloaded, MergedDataJson.Options));
    }

    [Fact]
    public async Task Loading_a_key_never_saved_returns_null()
    {
        var module = JSInterop.SetupModule("./js/indexedDbInterop.js");
        module.Setup<string?>("getDocument", _ => true).SetResult((string?)null);

        var store = new IndexedDbRulesDataStore(JSInterop.JSRuntime);

        var result = await store.LoadAsync<MergedRulesData>("unknown-key");

        Assert.Null(result);
    }
}
