using Servitor.App.ArmyBuilder.State;
using Servitor.App.ArmyImport.Parsing;
using Servitor.App.ArmyImport.Resolution;
using Servitor.App.RulesData.Loading;

namespace Servitor.App.BattleTracker.Opponent;

public sealed class OpponentArmyState(IMergedRulesDataProvider rulesData, IDatasheetCatalog catalog) : IOpponentArmyState
{
    private readonly Dictionary<Guid, int> _tokens = [];
    private readonly HashSet<Guid> _destroyed = [];

    public event Action? Changed;

    public IArmyBuilderState Army { get; private set; } = new ArmyBuilderState();

    public bool HasArmy => Army.Units.Count > 0;

    public async Task<ImportResolveResult> LoadAsync(string pastedText, CancellationToken cancellationToken = default)
    {
        var parsed = WarscribeExportParser.Parse(pastedText);
        if (!parsed.IsSuccess)
        {
            return ImportResolveResult.Failure([.. parsed.Errors]);
        }

        var army = new ArmyBuilderState();
        var result = await ArmyExportResolver.ResolveAsync(parsed.Export!, army, rulesData, catalog, cancellationToken);
        if (result.IsSuccess)
        {
            Army = army;
            _tokens.Clear();
            _destroyed.Clear();
            Changed?.Invoke();
        }

        return result;
    }

    public void Clear()
    {
        Army = new ArmyBuilderState();
        _tokens.Clear();
        _destroyed.Clear();
        Changed?.Invoke();
    }

    public int Tokens(Guid unitId) => _tokens.GetValueOrDefault(unitId);

    public void AddToken(Guid unitId)
    {
        _tokens[unitId] = Tokens(unitId) + 1;
        Changed?.Invoke();
    }

    public void RemoveToken(Guid unitId)
    {
        if (Tokens(unitId) > 1)
        {
            _tokens[unitId] = Tokens(unitId) - 1;
        }
        else
        {
            _tokens.Remove(unitId);
        }

        Changed?.Invoke();
    }

    public bool IsDestroyed(Guid unitId) => _destroyed.Contains(unitId);

    public void ToggleDestroyed(Guid unitId)
    {
        if (!_destroyed.Remove(unitId))
        {
            _destroyed.Add(unitId);
        }

        Changed?.Invoke();
    }
}
