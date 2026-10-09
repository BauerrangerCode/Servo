using Servitor.App.BattleTracker.Units;
using Servitor.App.RulesData.Storage;

namespace Servitor.App.BattleTracker.State;

public sealed class UnitConditionState(IRulesDataStore store) : IUnitConditionState
{
    private const string Key = "battle/unit-conditions";

    private bool _isLoaded;
    private Dictionary<string, GroupCondition> _conditions = [];

    public event Action? Changed;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;
        if (await store.LoadAsync<Dictionary<string, GroupCondition>>(Key, cancellationToken) is { } stored)
        {
            _conditions = stored;
            Changed?.Invoke();
        }
    }

    public GroupCondition Of(string groupKey) => _conditions.GetValueOrDefault(groupKey) ?? GroupCondition.Fresh;

    public Task AdjustDamageAsync(string groupKey, ModelGroup group, int delta) =>
        UpdateAsync(groupKey, c => GroupWounds.Adjust(group, c, delta));

    public Task RestoreGroupAsync(string groupKey) => UpdateAsync(groupKey, GroupWounds.Restore);

    public Task AddTokenAsync(string groupKey) => UpdateAsync(groupKey, c => c with { Tokens = c.Tokens + 1 });

    public Task RemoveTokenAsync(string groupKey) => UpdateAsync(groupKey, c => c with { Tokens = Math.Max(0, c.Tokens - 1) });

    public Task ResetAsync()
    {
        _conditions = [];
        return SaveAsync();
    }

    private Task UpdateAsync(string groupKey, Func<GroupCondition, GroupCondition> change)
    {
        var next = change(Of(groupKey));
        if (next == GroupCondition.Fresh)
        {
            _conditions.Remove(groupKey);
        }
        else
        {
            _conditions[groupKey] = next;
        }

        return SaveAsync();
    }

    private async Task SaveAsync()
    {
        Changed?.Invoke();
        await store.SaveAsync(Key, new Dictionary<string, GroupCondition>(_conditions));
    }
}
