using Servitor.App.ArmyBuilder.Catalog;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.ArmyBuilder.Rules;

/// <summary>
/// Picks the units a detachment rule is about. Every field that is set has to match (names, keywords and roles each
/// match when the unit fits any one of the listed values); a selector with no field set matches every unit.
/// </summary>
/// <param name="Names">Datasheet names, compared ignoring case, punctuation and the Legends marker.</param>
/// <param name="Keywords">Keywords of the datasheet, compared ignoring case; the unit needs at least one of them.</param>
/// <param name="AllKeywords">Keywords the unit needs all of, e.g. its faction keyword together with another one.</param>
/// <param name="Roles">Catalog roles such as "EpicHero" or "Character".</param>
/// <param name="ExcludeNames">Datasheet names that never match, even when the other fields do.</param>
/// <param name="ExcludeKeywords">Keywords that make a unit never match.</param>
/// <param name="OutsideFaction">
/// True: only units the army's own faction does not list, i.e. units a detachment lets the army take from elsewhere.
/// False: only units of the army's own faction. Null: either.
/// </param>
public sealed record UnitSelector(
    IReadOnlyList<string> Names,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> AllKeywords,
    IReadOnlyList<UnitRole> Roles,
    IReadOnlyList<string> ExcludeNames,
    IReadOnlyList<string> ExcludeKeywords,
    bool? OutsideFaction)
{
    public static UnitSelector Any { get; } = new([], [], [], [], [], [], null);

    public bool Matches(Datasheet datasheet, string? armyFaction)
    {
        if (Names.Count > 0 && !Names.Any(n => SameName(n, datasheet.Name)))
        {
            return false;
        }

        if (Keywords.Count > 0 && !Keywords.Any(k => HasKeyword(datasheet, k)))
        {
            return false;
        }

        if (AllKeywords.Any(k => !HasKeyword(datasheet, k)))
        {
            return false;
        }

        if (Roles.Count > 0 && !Roles.Contains(UnitRoles.Of(datasheet)))
        {
            return false;
        }

        if (ExcludeNames.Any(n => SameName(n, datasheet.Name)) || ExcludeKeywords.Any(k => HasKeyword(datasheet, k)))
        {
            return false;
        }

        if (OutsideFaction is { } outside && armyFaction is not null)
        {
            return outside != datasheet.FactionSlugs.Contains(armyFaction);
        }

        return true;
    }

    private static bool SameName(string a, string b) => NameNormalizer.Normalize(a) == NameNormalizer.Normalize(b);

    private static bool HasKeyword(Datasheet datasheet, string keyword) =>
        datasheet.Keywords.Contains(keyword, StringComparer.OrdinalIgnoreCase);
}
