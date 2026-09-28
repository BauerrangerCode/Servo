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
}
