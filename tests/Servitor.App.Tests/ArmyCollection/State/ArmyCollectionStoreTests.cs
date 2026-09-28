using Servitor.App.ArmyCollection.State;
using Servitor.App.Tests.RulesData.Loading;

namespace Servitor.App.Tests.ArmyCollection.State;

public class ArmyCollectionStoreTests
{
    private static SavedArmy Sample(string name) => new(
        Guid.NewGuid(), name, DateTimeOffset.UtcNow, "orks", "Strike Force", [], [], []);

    [Fact]
    public async Task Starts_empty()
    {
        var store = new ArmyCollectionStore(new InMemoryRulesDataStore());

        Assert.Empty(await store.ListAsync());
    }

    [Fact]
    public async Task A_saved_army_is_listed_back()
    {
        var store = new ArmyCollectionStore(new InMemoryRulesDataStore());

        await store.SaveAsync(Sample("First"));
        var armies = await store.ListAsync();

        var saved = Assert.Single(armies);
        Assert.Equal("First", saved.Name);
    }

    [Fact]
    public async Task Saving_multiple_armies_keeps_all_of_them()
    {
        var store = new ArmyCollectionStore(new InMemoryRulesDataStore());

        await store.SaveAsync(Sample("First"));
        await store.SaveAsync(Sample("Second"));

        var armies = await store.ListAsync();
        Assert.Equal(["First", "Second"], armies.Select(a => a.Name));
    }

    [Fact]
    public async Task Deleting_an_army_removes_only_that_one()
    {
        var store = new ArmyCollectionStore(new InMemoryRulesDataStore());
        var first = Sample("First");
        var second = Sample("Second");
        await store.SaveAsync(first);
        await store.SaveAsync(second);

        await store.DeleteAsync(first.Id);

        var armies = await store.ListAsync();
        var remaining = Assert.Single(armies);
        Assert.Equal("Second", remaining.Name);
    }

    [Fact]
    public async Task Deleting_an_unknown_id_is_a_no_op()
    {
        var store = new ArmyCollectionStore(new InMemoryRulesDataStore());
        await store.SaveAsync(Sample("First"));

        await store.DeleteAsync(Guid.NewGuid());

        Assert.Single(await store.ListAsync());
    }

    [Fact]
    public async Task Saving_an_army_with_the_same_id_replaces_it_instead_of_duplicating()
    {
        var store = new ArmyCollectionStore(new InMemoryRulesDataStore());
        var original = Sample("First");
        await store.SaveAsync(original);
        await store.SaveAsync(Sample("Second"));

        var overwritten = original with { BattleSizeName = "Onslaught" };
        await store.SaveAsync(overwritten);

        var armies = await store.ListAsync();
        Assert.Equal(2, armies.Count);
        var replaced = Assert.Single(armies, a => a.Id == original.Id);
        Assert.Equal("Onslaught", replaced.BattleSizeName);
    }
}
