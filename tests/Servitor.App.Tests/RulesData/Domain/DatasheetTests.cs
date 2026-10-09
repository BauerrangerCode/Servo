using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.RulesData.Domain;

public class DatasheetTests
{
    // Shared collection instances: IReadOnlyList<T> properties compare by reference under record-generated
    // equality (List<T>/arrays have no value equality of their own), so equality tests reuse these rather than
    // rebuilding equivalent-but-distinct lists on every call.
    private static readonly IReadOnlyList<WeaponProfile> RangedWeapons =
        [new WeaponProfile("Lasgun", WeaponType.Ranged, "24\"", "1", "4+", "3", "0", "1", ["Rapid Fire 1"])];

    private static readonly IReadOnlyList<Ability> NamedAbilities =
        [new Ability("Shock Troops", "Holds objectives.", AbilityCategory.Named)];

    private static readonly IReadOnlyList<string> Keywords = ["Infantry", "Battleline"];
    private static readonly IReadOnlyList<WeaponProfile> NoWeapons = [];
    private static readonly IReadOnlyList<Ability> NoAbilities = [];

    private static Datasheet CreateShockTrooper() => new(
        Name: "Cadian Shock Troops",
        Faction: "Imperium - Astra Militarum",
        Points: 70,
        StatLine: new StatLine("6\"", "3", "5+", null, "1", "7+", "2"),
        RangedWeapons: RangedWeapons,
        MeleeWeapons: NoWeapons,
        NamedAbilities: NamedAbilities,
        WargearAbilities: NoAbilities,
        CoreAbilities: NoAbilities,
        Keywords: Keywords,
        CompositionConstraint: new CompositionConstraint(6, 4),
        StratagemIds: []);

    [Fact]
    public void Construction_exposes_all_supplied_values()
    {
        var datasheet = CreateShockTrooper();

        Assert.Equal("Cadian Shock Troops", datasheet.Name);
        Assert.Equal(70, datasheet.Points);
        Assert.Equal("6\"", datasheet.StatLine.Movement);
        Assert.Single(datasheet.RangedWeapons);
        Assert.Equal(6, datasheet.CompositionConstraint?.StrikeForceAndOnslaughtLimit);
        Assert.Equal(4, datasheet.CompositionConstraint?.IncursionLimit);
    }

    [Fact]
    public void Records_with_equal_values_are_equal()
    {
        Assert.Equal(CreateShockTrooper(), CreateShockTrooper());
    }

    [Fact]
    public void Records_with_different_values_are_not_equal()
    {
        var a = CreateShockTrooper();
        var b = a with { Points = 75 };

        Assert.NotEqual(a, b);
    }
}
