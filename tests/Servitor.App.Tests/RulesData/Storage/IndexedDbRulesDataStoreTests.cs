using System.Text.Json;
using Bunit;
using Servitor.App.RulesData.Domain;
using Servitor.App.RulesData.Storage;

namespace Servitor.App.Tests.RulesData.Storage;

public class IndexedDbRulesDataStoreTests : BunitContext
{
    private static FactionRulesData CreateSampleFactionData() => new(
        FactionId: "astra-militarum",
        DataVersion: "2026-09-17",
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
                new CompositionConstraint(6, 4)),
        ],
        Stratagems: [new Stratagem("s1", "Command Re-roll", StratagemCategory.Other, 1, "", "Any", "Any Phase", "Re-roll a roll.", "")],
        Abilities: [new DetachmentAbility("a1", "Steel Discipline", "", "Some passive rule.", "Combined Regiment")],
        Enhancements: [new Enhancement("e1", "Kurov's Aquila", "", "Ignore the first mortal wound.", 30, "Combined Regiment", false, null)]);

    [Fact]
    public async Task Saved_faction_data_round_trips_through_the_store_unchanged()
    {
        var module = JSInterop.SetupModule("./js/indexedDbInterop.js");
        module.SetupVoid("putFaction", _ => true).SetVoidResult();

        var original = CreateSampleFactionData();
        var store = new IndexedDbRulesDataStore(JSInterop.JSRuntime);

        await store.SaveFactionAsync(original);

        var savedInvocation = Assert.Single(module.Invocations, i => i.Identifier == "putFaction");
        var savedJson = Assert.IsType<string>(savedInvocation.Arguments[1]);

        module.Setup<string?>("getFaction", _ => true).SetResult(savedJson);

        var reloaded = await store.LoadFactionAsync("astra-militarum", "2026-09-17");

        // Record-generated equality compares IReadOnlyList<T> properties by reference, which two independently
        // deserialized object graphs never share — compare content via their canonical JSON form instead.
        Assert.NotNull(reloaded);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(reloaded));
    }

    [Fact]
    public async Task Loading_a_faction_never_saved_returns_null()
    {
        var module = JSInterop.SetupModule("./js/indexedDbInterop.js");
        module.Setup<string?>("getFaction", _ => true).SetResult((string?)null);

        var store = new IndexedDbRulesDataStore(JSInterop.JSRuntime);

        var result = await store.LoadFactionAsync("unknown-faction", "v1");

        Assert.Null(result);
    }
}
