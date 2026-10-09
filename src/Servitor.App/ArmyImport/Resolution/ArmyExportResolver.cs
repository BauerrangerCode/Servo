using Servitor.App.ArmyBuilder.Catalog;
using Servitor.App.ArmyBuilder.State;
using Servitor.App.ArmyImport.Parsing;
using Servitor.App.RulesData.Loading;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.ArmyImport.Resolution;

/// <summary>
/// Resolves a <see cref="ParsedArmyExport"/> (pure text, see <c>WarscribeExportParser</c>) against the merged
/// rules-data catalog and pushes the result into <see cref="IArmyBuilderState"/> via the same methods Build (E3)
/// itself calls — so an imported army is built exactly like a manually-built one, sharing its validation.
/// </summary>
public static class ArmyExportResolver
{
    public static async Task<ImportResolveResult> ResolveAsync(
        ParsedArmyExport export,
        IArmyBuilderState state,
        IMergedRulesDataProvider rulesDataProvider,
        IDatasheetCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        var rulesData = await rulesDataProvider.GetAsync(cancellationToken);
        if (rulesData is null)
        {
            return ImportResolveResult.Failure(["No rules data is available yet — use \"Merge data\" in the top bar first."]);
        }

        var allDatasheets = await catalog.GetAllAsync(cancellationToken);
        var errors = new List<string>();

        var factionId = ResolveFactionId(export.FactionKeyword, allDatasheets, rulesData);
        var faction = factionId is null ? null : rulesData.Factions.FirstOrDefault(f => f.FactionId == factionId);
        if (faction is null)
        {
            errors.Add($"Could not resolve faction \"{export.FactionKeyword}\" against the loaded rules data.");
            return ImportResolveResult.Failure(errors);
        }

        var detachments = new List<Detachment>();
        foreach (var name in export.DetachmentNames)
        {
            var detachment = CatalogNameResolver.FindByName(faction.Detachments, d => d.Name, name);
            if (detachment is null)
            {
                errors.Add($"Unknown detachment \"{name}\" for faction \"{faction.Name}\".");
            }
            else
            {
                detachments.Add(detachment);
            }
        }

        var factionDatasheets = allDatasheets.Where(d => d.FactionSlugs.Contains(factionId)).ToList();
        var availableEnhancements = detachments.SelectMany(d => faction.Enhancements.Where(e => e.Detachment == d.Name)).ToList();

        var characterPlans = new List<(ParsedCharacter Parsed, Datasheet Datasheet, UnitSize Size, Enhancement? Enhancement)>();
        foreach (var character in export.Characters)
        {
            var plan = CatalogNameResolver.ResolveUnitPlan(character.Name, 1, factionDatasheets, errors);
            var enhancement = ResolveEnhancement(character.Enhancement, $"Char{character.CharacterNumber}: {character.Name}", availableEnhancements, errors);
            if (plan is { } p)
            {
                characterPlans.Add((character, p.Datasheet, p.Size, enhancement));
            }
        }

        var unitPlans = new List<(ParsedUnit Parsed, Datasheet Datasheet, UnitSize Size, Enhancement? Enhancement)>();
        foreach (var unit in export.Units)
        {
            var plan = CatalogNameResolver.ResolveUnitPlan(unit.Name, unit.ModelCount, factionDatasheets, errors);
            var enhancement = ResolveEnhancement(unit.Enhancement, $"{unit.ModelCount}x {unit.Name}", availableEnhancements, errors);
            if (plan is { } p)
            {
                unitPlans.Add((unit, p.Datasheet, p.Size, enhancement));
            }
        }

        // Everything above only reads the catalog — nothing on `state` is touched until every name in the
        // pasted text has resolved, so a single unknown unit/detachment/enhancement fails the whole import
        // instead of leaving Build half-populated with only some of the list applied.
        if (errors.Count > 0)
        {
            return ImportResolveResult.Failure(errors);
        }

        // Reset first: IArmyBuilderState is shared across Import/Build/Play, so it may already hold this exact
        // faction (e.g. an existing Build in progress) — SelectFaction alone is a no-op for units/detachments when
        // the faction doesn't change, which would otherwise make every add below fail as AlreadySelected/AtLimit
        // against the leftover army instead of the one being imported.
        state.Reset();
        state.SelectFaction(factionId!, faction.AllyFactionIds);
        state.SelectBattleSize(ResolveBattleSize(export.TotalArmyPoints));

        foreach (var detachment in detachments)
        {
            var result = state.AddDetachment(detachment);
            if (result != DetachmentAddResult.Added)
            {
                errors.Add($"Could not add detachment \"{detachment.Name}\": {result}.");
            }
        }

        // Keyed by character name: the export format itself only records a leader attachment by display name (no
        // Char<N> cross-reference on the unit side), so two characters sharing an identical name are inherently
        // ambiguous in the source text, not just in this resolver — not seen in practice (character names are
        // normally unique), so not specially handled beyond last-one-wins.
        var characterUnitsByName = new Dictionary<string, ArmyUnit>();
        Guid? warlordUnitId = null;

        foreach (var (parsed, datasheet, size, enhancement) in characterPlans)
        {
            var addResult = state.AddUnit(datasheet, size);
            if (addResult != AddUnitResult.Added)
            {
                errors.Add($"Could not add \"{parsed.Name}\": {addResult}.");
                continue;
            }

            var unit = state.Units[^1];
            characterUnitsByName[parsed.Name] = unit;

            if (enhancement is not null)
            {
                state.SetEnhancement(unit.Id, enhancement);
            }

            if (parsed.IsWarlord)
            {
                warlordUnitId = unit.Id;
            }
        }

        foreach (var (parsed, datasheet, size, enhancement) in unitPlans)
        {
            var addResult = state.AddUnit(datasheet, size);
            if (addResult != AddUnitResult.Added)
            {
                errors.Add($"Could not add \"{parsed.Name}\" ({parsed.ModelCount}x): {addResult}.");
                continue;
            }

            var unit = state.Units[^1];

            if (enhancement is not null)
            {
                state.SetEnhancement(unit.Id, enhancement);
            }

            if (parsed.LedByCharacterName is not null && characterUnitsByName.TryGetValue(parsed.LedByCharacterName, out var leader))
            {
                state.SetLeader(unit.Id, leader.Id);
            }
        }

        if (warlordUnitId is { } id)
        {
            state.SetWarlord(id);
        }

        return errors.Count > 0 ? ImportResolveResult.Failure(errors) : ImportResolveResult.Success(export.Warnings);
    }

    // The header's FACTION KEYWORD is close to, but not always identical to, Datasheet.Faction: Warscribe drops
    // the chapter-tier middle segment BSData keeps for Space Marine successors (header "Imperium - Space Wolves"
    // vs. Datasheet.Faction "Imperium - Adeptus Astartes - Space Wolves"), and several successor chapters merge
    // into one shared "space-marines" faction slug anyway. Matching on the outer group and the specific name (the
    // first and last " - "-separated segments) rather than exact equality handles both without needing a
    // hardcoded chapter list; resolved via the FactionSlugs any matching datasheet carries, picking the slug
    // shared by the most of them in case a catalogue maps to more than one (e.g. Deathwatch).
    private static string? ResolveFactionId(string factionKeyword, IReadOnlyList<Datasheet> allDatasheets, MergedRulesData rulesData)
    {
        var wanted = SplitFactionKeyword(factionKeyword);
        return allDatasheets
            .Where(d => SplitFactionKeyword(d.Faction) == wanted)
            .SelectMany(d => d.FactionSlugs)
            .GroupBy(s => s)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault(slug => rulesData.Factions.Any(f => f.FactionId == slug));
    }

    private static (string Group, string Name) SplitFactionKeyword(string keyword)
    {
        var parts = keyword.Split(" - ", StringSplitOptions.TrimEntries);
        return (parts[0], parts[^1]);
    }

    // The export format has no explicit Battle Size field — inferred from the declared total points, since every
    // sample's detachment/DP composition matches the bracket its points total falls into.
    private static BattleSize ResolveBattleSize(int totalPoints) =>
        BattleSizes.All.FirstOrDefault(b => totalPoints <= b.PointsLimit) ?? BattleSizes.Onslaught;

    private static Enhancement? ResolveEnhancement(
        ParsedEnhancement? parsed, string ownerLabel, IReadOnlyList<Enhancement> available, List<string> errors)
    {
        if (parsed is null)
        {
            return null;
        }

        var match = CatalogNameResolver.FindByName(available, e => e.Name, parsed.Name);
        if (match is null)
        {
            errors.Add($"Unknown enhancement \"{parsed.Name}\" on {ownerLabel}.");
            return null;
        }

        if (match.IsUpgrade != parsed.HasUpgradeQualifier)
        {
            errors.Add(
                $"Enhancement \"{parsed.Name}\" on {ownerLabel}: the \"(Upgrade)\" qualifier doesn't match the catalog.");
        }

        if (match.Cost != parsed.Points)
        {
            errors.Add(
                $"Enhancement \"{parsed.Name}\" on {ownerLabel}: parsed +{parsed.Points}pts doesn't match the catalog's {match.Cost}pts.");
        }

        return match;
    }

}
