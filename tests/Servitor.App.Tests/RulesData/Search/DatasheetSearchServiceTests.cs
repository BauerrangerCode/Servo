using Servitor.App.RulesData.Search;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.RulesData.Search;

public class DatasheetSearchServiceTests
{
    private static Datasheet Sheet(
        string name,
        string faction = "Astra Militarum",
        string toughness = "3",
        string wounds = "1",
        IReadOnlyList<string>? keywords = null,
        IReadOnlyList<Ability>? namedAbilities = null,
        IReadOnlyList<WeaponProfile>? rangedWeapons = null) => new(
        name,
        faction,
        70,
        new StatLine("6\"", toughness, "5+", null, wounds, "7+", "2"),
        rangedWeapons ?? [],
        [],
        namedAbilities ?? [],
        [],
        [],
        keywords ?? ["Infantry", "Battleline"],
        null,
        []);

    private static readonly Datasheet ShockTroops = Sheet("Cadian Shock Troops");
    private static readonly Datasheet Bullgryns = Sheet(
        "Bullgryn Squad", keywords: ["Infantry"], toughness: "6",
        namedAbilities: [new Ability("Slabshield", "Feel No Pain 5+.", AbilityCategory.Named)]);
    private static readonly Datasheet Leman = Sheet(
        "Leman Russ Battle Tank", keywords: ["Vehicle"], toughness: "11", wounds: "13",
        rangedWeapons: [new WeaponProfile("Battle cannon", WeaponType.Ranged, "72\"", "D6", "3+", "9", "-2", "3", ["Blast"])]);
    private static readonly Datasheet Legends = Sheet("Munitorum Servitors [Legends]", keywords: ["Infantry"]);
    private static readonly Datasheet Xenos = Sheet("Boyz", faction: "Orks", keywords: ["Infantry", "Battleline"]);

    private static readonly List<Datasheet> All = [ShockTroops, Bullgryns, Leman, Legends, Xenos];

    private static DatasheetSearchCriteria Criteria(
        string query = "",
        DatasheetSearchMode mode = DatasheetSearchMode.All,
        Dictionary<string, TriState>? roles = null,
        Dictionary<string, TriState>? factions = null,
        bool showLegends = false,
        CharacteristicFilter? characteristic = null) =>
        new(query, mode, roles ?? [], factions ?? [], showLegends, characteristic);

    [Fact]
    public void No_criteria_returns_every_non_legends_datasheet()
    {
        var result = DatasheetSearchService.Search(All, Criteria());

        Assert.Equal([ShockTroops, Bullgryns, Leman, Xenos], result);
    }

    [Fact]
    public void Legends_toggle_shows_only_legends_datasheets()
    {
        var result = DatasheetSearchService.Search(All, Criteria(showLegends: true));

        Assert.Equal([Legends], result);
    }

    [Fact]
    public void Name_mode_only_matches_the_name()
    {
        var result = DatasheetSearchService.Search(All, Criteria("Bullgryn", DatasheetSearchMode.Name));

        Assert.Equal([Bullgryns], result);
    }

    [Fact]
    public void Name_mode_does_not_match_ability_text()
    {
        var result = DatasheetSearchService.Search(All, Criteria("Feel No Pain", DatasheetSearchMode.Name));

        Assert.Empty(result);
    }

    [Fact]
    public void RulesText_mode_matches_an_ability_description()
    {
        var result = DatasheetSearchService.Search(All, Criteria("Feel No Pain", DatasheetSearchMode.RulesText));

        Assert.Equal([Bullgryns], result);
    }

    [Fact]
    public void RulesText_mode_matches_a_weapon_keyword()
    {
        var result = DatasheetSearchService.Search(All, Criteria("Blast", DatasheetSearchMode.RulesText));

        Assert.Equal([Leman], result);
    }

    [Fact]
    public void All_mode_combines_name_and_rules_text_matches()
    {
        var byName = DatasheetSearchService.Search(All, Criteria("Cadian", DatasheetSearchMode.All));
        var byRulesText = DatasheetSearchService.Search(All, Criteria("Blast", DatasheetSearchMode.All));

        Assert.Equal([ShockTroops], byName);
        Assert.Equal([Leman], byRulesText);
    }

    [Fact]
    public void Role_include_filter_keeps_only_matching_keywords()
    {
        var roles = new Dictionary<string, TriState> { ["Vehicle"] = TriState.Include };

        var result = DatasheetSearchService.Search(All, Criteria(roles: roles));

        Assert.Equal([Leman], result);
    }

    [Fact]
    public void Role_exclude_filter_removes_matching_keywords()
    {
        var roles = new Dictionary<string, TriState> { ["Vehicle"] = TriState.Exclude };

        var result = DatasheetSearchService.Search(All, Criteria(roles: roles));

        Assert.DoesNotContain(Leman, result);
        Assert.Contains(ShockTroops, result);
    }

    [Fact]
    public void Faction_filter_combines_with_role_filter_using_and_semantics()
    {
        var roles = new Dictionary<string, TriState> { ["Battleline"] = TriState.Include };
        var factions = new Dictionary<string, TriState> { ["Orks"] = TriState.Include };

        var result = DatasheetSearchService.Search(All, Criteria(roles: roles, factions: factions));

        Assert.Equal([Xenos], result);
    }

    [Fact]
    public void Characteristic_filter_keeps_only_datasheets_at_or_above_the_minimum()
    {
        var filter = new CharacteristicFilter(CharacteristicKind.Toughness, 10);

        var result = DatasheetSearchService.Search(All, Criteria(characteristic: filter));

        Assert.Equal([Leman], result);
    }

    [Fact]
    public void Characteristic_filter_can_target_wounds_instead()
    {
        var filter = new CharacteristicFilter(CharacteristicKind.Wounds, 5);

        var result = DatasheetSearchService.Search(All, Criteria(characteristic: filter));

        Assert.Equal([Leman], result);
    }
}
