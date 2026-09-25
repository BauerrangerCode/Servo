using Servitor.RulesData.Domain;

namespace Servitor.App.ArmyBuilder.Catalog;

/// <summary>The groups of the unit catalog, in display order.</summary>
public enum UnitRole
{
    EpicHero,
    Character,
    Battleline,
    Transport,
    Fortification,
    Other,
}

public static class UnitRoles
{
    public static IReadOnlyList<UnitRole> All { get; } = Enum.GetValues<UnitRole>();

    /// <summary>The most specific role the unit's keywords give it: an Epic Hero is also a Character.</summary>
    public static UnitRole Of(Datasheet datasheet)
    {
        bool Has(string keyword) => datasheet.Keywords.Contains(keyword, StringComparer.OrdinalIgnoreCase);

        if (Has("Epic Hero"))
        {
            return UnitRole.EpicHero;
        }

        if (Has("Character"))
        {
            return UnitRole.Character;
        }

        if (Has("Battleline"))
        {
            return UnitRole.Battleline;
        }

        if (Has("Dedicated Transport"))
        {
            return UnitRole.Transport;
        }

        return Has("Fortification") ? UnitRole.Fortification : UnitRole.Other;
    }

    public static string Label(UnitRole role) => role switch
    {
        UnitRole.EpicHero => "Epic Hero",
        _ => role.ToString(),
    };
}
