using Servitor.App.RulesData.Storage;

namespace Servitor.App.BattleTracker.State;

public sealed class ArmyRuleState(IRulesDataStore store) : IArmyRuleState
{
    private const string Key = "battle/army-rules";

    private bool _isLoaded;
    private bool _showAuras = true;
    private Dictionary<string, string> _pins = [];
    private Dictionary<string, string> _activeOptions = [];
    private Dictionary<string, List<string>> _usedOptions = [];

    public event Action? Changed;

    public bool ShowAuras => _showAuras;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;
        if (await store.LoadAsync<ArmyRuleSettings>(Key, cancellationToken) is { } stored)
        {
            _showAuras = stored.ShowAuras;
            _pins = new Dictionary<string, string>(stored.Pins);
            _activeOptions = new Dictionary<string, string>(stored.ActiveOptions);
            _usedOptions = stored.UsedOptions.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
            Changed?.Invoke();
        }
    }

    public Task SetShowAurasAsync(bool show)
    {
        _showAuras = show;
        return SaveAsync();
    }

    public bool IsPinned(string ruleId) => _pins.ContainsKey(ruleId);

    public string PinNote(string ruleId) => _pins.GetValueOrDefault(ruleId, "");

    public Task PinAsync(string ruleId)
    {
        _pins.TryAdd(ruleId, "");
        return SaveAsync();
    }

    public Task UnpinAsync(string ruleId)
    {
        _pins.Remove(ruleId);
        return SaveAsync();
    }

    public Task SetPinNoteAsync(string ruleId, string note)
    {
        _pins[ruleId] = note;
        return SaveAsync();
    }

    public string? ActiveOption(string ruleId) => _activeOptions.GetValueOrDefault(ruleId);

    public IReadOnlyList<string> UsedOptions(string ruleId) => _usedOptions.GetValueOrDefault(ruleId) ?? [];

    public Task ActivateOptionAsync(string ruleId, string option)
    {
        EndActive(ruleId);
        _activeOptions[ruleId] = option;
        return SaveAsync();
    }

    public Task EndOptionAsync(string ruleId)
    {
        EndActive(ruleId);
        return SaveAsync();
    }

    private void EndActive(string ruleId)
    {
        if (!_activeOptions.Remove(ruleId, out var ended))
        {
            return;
        }

        if (!_usedOptions.TryGetValue(ruleId, out var used))
        {
            _usedOptions[ruleId] = used = [];
        }

        if (!used.Contains(ended))
        {
            used.Add(ended);
        }
    }

    private async Task SaveAsync()
    {
        Changed?.Invoke();
        await store.SaveAsync(Key, new ArmyRuleSettings(
            _showAuras,
            new Dictionary<string, string>(_pins),
            new Dictionary<string, string>(_activeOptions),
            _usedOptions.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value.ToList())));
    }
}
