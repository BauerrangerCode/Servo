using System.Text.Json;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Mapping.BsData;

namespace Servitor.RulesData.Mapping;

public sealed record BsDataMappingResult(IReadOnlyList<Datasheet> Datasheets, IReadOnlyList<string> Warnings);

/// <summary>
/// Maps a BSData wh40k-11e catalogue JSON document into <see cref="Datasheet"/> domain records, so the rest of
/// the app never depends on BattleScribe's external, recursive entry/entry-link/shared-profile shape directly.
/// </summary>
public static class BsDataCatalogueMapper
{
    private const string UnitStatProfileTypeName = "Unit";
    private const string RangedWeaponsProfileTypeName = "Ranged Weapons";
    private const string MeleeWeaponsProfileTypeName = "Melee Weapons";
    private const string AbilitiesProfileTypeName = "Abilities";

    // BSData represents multi-model squad datasheets as "unit" and single-model datasheets (characters,
    // vehicles, walkers, monsters) as "model" — both are standalone, playable datasheets with an identical
    // entry shape (profiles/costs/constraints/entryLinks/selectionEntryGroups), so both must be mapped.
    private const string UnitEntryType = "unit";
    private const string ModelEntryType = "model";

    public static BsDataMappingResult Map(string catalogueJson)
    {
        var warnings = new List<string>();

        BsCatalogueDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<BsCatalogueDocument>(catalogueJson);
        }
        catch (JsonException ex)
        {
            return new BsDataMappingResult([], [$"Catalogue is not valid JSON: {ex.Message}"]);
        }

        var catalogue = document?.Catalogue;
        if (catalogue is null)
        {
            return new BsDataMappingResult([], ["Catalogue document has no 'catalogue' root."]);
        }

        var faction = NormalizeFactionName(catalogue.Name ?? "Unknown Faction");
        var entriesById = new Dictionary<string, BsEntry>();
        var profilesById = new Dictionary<string, BsProfile>();
        foreach (var entry in catalogue.SharedSelectionEntries)
        {
            IndexEntries(entry, entriesById);
        }

        foreach (var profile in catalogue.SharedProfiles)
        {
            profilesById[profile.Id] = profile;
        }

        // Only direct (top-level) entries of sharedSelectionEntries are standalone datasheets. "model"-typed
        // entries also occur nested inside a unit's own subtree (e.g. per-model loadout variants within a
        // squad) — entriesById indexes those too, so filtering entriesById.Values here would wrongly promote
        // every nested loadout variant into its own datasheet instead of treating it as part of its parent.
        var datasheets = new List<Datasheet>();
        foreach (var entry in catalogue.SharedSelectionEntries.Where(e => e.Type is UnitEntryType or ModelEntryType))
        {
            try
            {
                datasheets.Add(MapUnit(entry, faction, entriesById, profilesById));
            }
            catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or NullReferenceException)
            {
                warnings.Add($"Skipped unit '{entry.Name}': {ex.Message}");
            }
        }

        return new BsDataMappingResult(datasheets, warnings);
    }

    private static void IndexEntries(BsEntry entry, Dictionary<string, BsEntry> entriesById)
    {
        if (!string.IsNullOrEmpty(entry.Id))
        {
            entriesById[entry.Id] = entry;
        }

        foreach (var child in entry.EntryLinks)
        {
            IndexEntries(child, entriesById);
        }

        foreach (var group in entry.SelectionEntryGroups)
        {
            IndexEntries(group, entriesById);
        }

        foreach (var child in entry.SelectionEntries)
        {
            IndexEntries(child, entriesById);
        }
    }

    private static Datasheet MapUnit(
        BsEntry unit,
        string faction,
        IReadOnlyDictionary<string, BsEntry> entriesById,
        IReadOnlyDictionary<string, BsProfile> profilesById)
    {
        var points = (int)(unit.Costs.FirstOrDefault(c => c.Name == "pts")?.Value ?? 0);
        var keywords = unit.CategoryLinks.Select(c => c.Name).ToList();

        StatLine? statLine = null;
        var rangedWeapons = new List<WeaponProfile>();
        var meleeWeapons = new List<WeaponProfile>();
        var wargearAbilities = new List<Ability>();
        var visited = new HashSet<string>();

        CollectFromSubtree(unit, isUnitItself: true);

        void CollectFromSubtree(BsEntry node, bool isUnitItself)
        {
            if (!string.IsNullOrEmpty(node.Id) && !visited.Add(node.Id))
            {
                return;
            }

            if (statLine is null)
            {
                // The "Unit" stat-line profile is either inlined directly on the model node, or referenced via
                // an infoLink into the catalogue's shared profile pool — different catalogue files use either
                // shape, so both are checked.
                var statProfile = node.Profiles.FirstOrDefault(p => p.TypeName == UnitStatProfileTypeName)
                    ?? node.InfoLinks
                        .Where(l => l.Type == "profile")
                        .Select(l => profilesById.GetValueOrDefault(l.TargetId))
                        .FirstOrDefault(p => p?.TypeName == UnitStatProfileTypeName);
                if (statProfile is not null)
                {
                    statLine = BuildStatLine(statProfile);
                }
            }

            foreach (var profile in node.Profiles)
            {
                if (profile.TypeName is RangedWeaponsProfileTypeName or MeleeWeaponsProfileTypeName)
                {
                    var weapon = BuildWeaponProfile(profile);
                    var target = profile.TypeName == RangedWeaponsProfileTypeName ? rangedWeapons : meleeWeapons;
                    if (target.All(w => w.Name != weapon.Name))
                    {
                        target.Add(weapon);
                    }
                }
                else if (profile.TypeName == AbilitiesProfileTypeName && !isUnitItself)
                {
                    wargearAbilities.Add(BuildAbility(profile, AbilityCategory.Wargear));
                }
            }

            foreach (var link in node.EntryLinks)
            {
                if (link.TargetId is { Length: > 0 } targetId && entriesById.TryGetValue(targetId, out var resolved))
                {
                    CollectFromSubtree(resolved, isUnitItself: false);
                }
            }

            foreach (var group in node.SelectionEntryGroups)
            {
                CollectFromSubtree(group, isUnitItself: false);
            }

            foreach (var child in node.SelectionEntries)
            {
                CollectFromSubtree(child, isUnitItself: false);
            }
        }

        var namedAbilities = unit.Profiles
            .Where(p => p.TypeName == AbilitiesProfileTypeName)
            .Select(p => BuildAbility(p, AbilityCategory.Named))
            .ToList();

        var coreAbilities = BuildCoreAbilities(unit);

        return new Datasheet(
            unit.Name,
            faction,
            points,
            statLine ?? new StatLine("-", "-", "-", null, "-", "-", "-"),
            rangedWeapons,
            meleeWeapons,
            namedAbilities,
            wargearAbilities,
            coreAbilities,
            keywords,
            BuildCompositionConstraint(unit),
            []);
    }

    private static StatLine BuildStatLine(BsProfile profile)
    {
        string Characteristic(string name) =>
            profile.Characteristics.FirstOrDefault(c => c.Name == name)?.Text ?? "-";

        var invulnerable = Characteristic("InSv");
        return new StatLine(
            Characteristic("M"),
            Characteristic("T"),
            Characteristic("Sv"),
            string.IsNullOrWhiteSpace(invulnerable) ? null : invulnerable,
            Characteristic("W"),
            Characteristic("LD"),
            Characteristic("OC"));
    }

    private static WeaponProfile BuildWeaponProfile(BsProfile profile)
    {
        string Characteristic(string name) =>
            profile.Characteristics.FirstOrDefault(c => c.Name == name)?.Text ?? "-";

        var isRanged = profile.TypeName == RangedWeaponsProfileTypeName;
        var skill = Characteristic(isRanged ? "BS" : "WS");
        var keywords = Characteristic("Keywords")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        return new WeaponProfile(
            profile.Name,
            isRanged ? WeaponType.Ranged : WeaponType.Melee,
            isRanged ? Characteristic("Range") : null,
            Characteristic("A"),
            skill,
            Characteristic("S"),
            Characteristic("AP"),
            Characteristic("D"),
            keywords);
    }

    private static Ability BuildAbility(BsProfile profile, AbilityCategory category)
    {
        var description = profile.Characteristics.FirstOrDefault(c => c.Name == "Description")?.Text ?? "";
        return new Ability(profile.Name, BsDataText.CleanMarkdown(description), category);
    }

    private static List<Ability> BuildCoreAbilities(BsEntry unit)
    {
        var coreAbilities = new List<Ability>();
        foreach (var tag in new[] { "Leader", "Support" })
        {
            if (unit.CategoryLinks.Any(c => c.Name == tag))
            {
                coreAbilities.Add(new Ability(tag.ToUpperInvariant(), "", AbilityCategory.Core));
            }
        }

        return coreAbilities;
    }

    private static CompositionConstraint? BuildCompositionConstraint(BsEntry unit)
    {
        var baseConstraint = unit.Constraints.FirstOrDefault(c =>
            c.Field == "selections" && c.Type == "max" && c.Scope is "force" or "roster"
            && c.Value.AsInt() is not null and not -1);
        var baseLimit = baseConstraint?.Value.AsInt();
        if (baseConstraint is null || baseLimit is null)
        {
            return null;
        }

        var incursionOverride = unit.Modifiers.FirstOrDefault(m =>
            m.Field == baseConstraint.Id
            && m.Type == "set"
            && m.Conditions.Any(c => c.ChildName?.Contains("Incursion", StringComparison.OrdinalIgnoreCase) == true));

        return new CompositionConstraint(baseLimit.Value, incursionOverride?.Value.AsInt());
    }

    private static string NormalizeFactionName(string catalogueName) =>
        catalogueName.EndsWith(" - Library", StringComparison.OrdinalIgnoreCase)
            ? catalogueName[..^" - Library".Length]
            : catalogueName;
}
