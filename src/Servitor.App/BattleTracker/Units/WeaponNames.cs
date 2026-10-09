using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.BattleTracker.Units;

/// <summary>
/// An army list names a weapon ("Kombi-skorcha"); the datasheet lists its profiles ("➤ Kombi-skorcha - Skorcha",
/// "➤ Kombi-skorcha - Shoota"). This joins the two by the name in front of the profile suffix.
/// </summary>
public static class WeaponNames
{
    private const string ProfileMarker = "➤";
    private const string ProfileSeparator = " - ";

    public static string Key(string name) => NameNormalizer.Normalize(name);

    /// <summary>The weapon a profile belongs to: the profile name without its marker and its " - mode" suffix.</summary>
    public static string BaseName(string profileName)
    {
        var name = profileName.Trim().TrimStart(ProfileMarker.ToCharArray()).Trim();
        var separator = name.IndexOf(ProfileSeparator, StringComparison.Ordinal);
        return separator > 0 ? name[..separator] : name;
    }

    public static IEnumerable<WeaponProfile> AllProfiles(Datasheet datasheet) => datasheet.RangedWeapons.Concat(datasheet.MeleeWeapons);

    public static IReadOnlyList<WeaponProfile> ProfilesOf(Datasheet datasheet, string weaponName)
    {
        var key = Key(weaponName);
        return [.. AllProfiles(datasheet).Where(p => Key(BaseName(p.Name)) == key || Key(p.Name) == key)];
    }
}
