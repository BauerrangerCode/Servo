namespace Servitor.App.RulesData.Search;

public sealed record DatasheetSearchCriteria(
    string Query,
    DatasheetSearchMode Mode,
    IReadOnlyDictionary<string, TriState> RoleFilters,
    IReadOnlyDictionary<string, TriState> FactionFilters,
    bool ShowLegends,
    CharacteristicFilter? CharacteristicFilter)
{
    public static DatasheetSearchCriteria Empty { get; } = new(
        Query: "",
        Mode: DatasheetSearchMode.All,
        RoleFilters: new Dictionary<string, TriState>(),
        FactionFilters: new Dictionary<string, TriState>(),
        ShowLegends: false,
        CharacteristicFilter: null);
}
