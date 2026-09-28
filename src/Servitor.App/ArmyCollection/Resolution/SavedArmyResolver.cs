using Servitor.App.ArmyBuilder.Catalog;
using Servitor.App.ArmyBuilder.State;
using Servitor.App.ArmyCollection.State;
using Servitor.App.RulesData.Loading;
using Servitor.RulesData.Domain;

namespace Servitor.App.ArmyCollection.Resolution;

/// <summary>
/// Resolves a <see cref="SavedArmy"/> (references by name, see the type) against the current rules-data catalog
/// and pushes it into <see cref="IArmyBuilderState"/> via the same methods Build (E3) and the Warscribe import
/// resolver (E4.2) use, so a reopened army is built and validated exactly like any other.
/// </summary>
public static class SavedArmyResolver
{
    public static async Task<OpenSavedArmyResult> ResolveAsync(
        SavedArmy saved,
        IArmyBuilderState state,
        IMergedRulesDataProvider rulesDataProvider,
        IDatasheetCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        var rulesData = await rulesDataProvider.GetAsync(cancellationToken);
        if (rulesData is null)
        {
            return OpenSavedArmyResult.Failure(["No rules data is available yet — use \"Merge data\" in the top bar first."]);
        }

        var faction = rulesData.Factions.FirstOrDefault(f => f.FactionId == saved.FactionId);
        if (faction is null)
        {
            return OpenSavedArmyResult.Failure([$"Faction \"{saved.FactionId}\" is no longer in the rules data."]);
        }

        var battleSize = BattleSizes.All.FirstOrDefault(b => b.Name == saved.BattleSizeName);
        if (battleSize is null)
        {
            return OpenSavedArmyResult.Failure([$"Unknown battle size \"{saved.BattleSizeName}\"."]);
        }

        var errors = new List<string>();

        var detachments = new List<Detachment>();
        foreach (var name in saved.DetachmentNames)
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

        var allDatasheets = await catalog.GetAllAsync(cancellationToken);
        var availableEnhancements = detachments.SelectMany(d => faction.Enhancements.Where(e => e.Detachment == d.Name)).ToList();

        var unitPlans = new List<(SavedUnit Saved, Datasheet Datasheet, UnitSize Size, Enhancement? Enhancement)>();
        foreach (var unit in saved.Units)
        {
            var unitFactionId = unit.AlliedFaction ?? saved.FactionId;
            var candidateDatasheets = allDatasheets.Where(d => d.FactionSlugs.Contains(unitFactionId)).ToList();
            var plan = CatalogNameResolver.ResolveUnitPlan(unit.DatasheetName, unit.ModelCount, candidateDatasheets, errors);

            Enhancement? enhancement = null;
            if (unit.EnhancementName is { } enhancementName)
            {
                enhancement = CatalogNameResolver.FindByName(availableEnhancements, e => e.Name, enhancementName);
                if (enhancement is null)
                {
                    errors.Add($"Unknown enhancement \"{enhancementName}\" on \"{unit.DatasheetName}\".");
                }
            }

            if (plan is { } p)
            {
                unitPlans.Add((unit, p.Datasheet, p.Size, enhancement));
            }
        }

        // As with the import resolver: nothing on `state` is touched until every reference in the saved army has
        // resolved, so a stale reference (e.g. a datasheet removed by a rules-data update) fails the whole reopen
        // instead of leaving Build half-populated.
        if (errors.Count > 0)
        {
            return OpenSavedArmyResult.Failure(errors);
        }

        state.SelectFaction(saved.FactionId, saved.AllyFactionIds);
        state.SelectBattleSize(battleSize);

        foreach (var detachment in detachments)
        {
            var result = state.AddDetachment(detachment);
            if (result != DetachmentAddResult.Added)
            {
                errors.Add($"Could not add detachment \"{detachment.Name}\": {result}.");
            }
        }

        var idMap = new Dictionary<Guid, Guid>();
        foreach (var (plannedUnit, datasheet, size, enhancement) in unitPlans)
        {
            var addResult = state.AddUnit(datasheet, size, plannedUnit.AlliedFaction);
            if (addResult != AddUnitResult.Added)
            {
                errors.Add($"Could not add \"{plannedUnit.DatasheetName}\": {addResult}.");
                continue;
            }

            var unit = state.Units[^1];
            idMap[plannedUnit.Id] = unit.Id;

            if (plannedUnit.Wargear != WargearSelection.Default)
            {
                state.SetWargear(unit.Id, plannedUnit.Wargear);
            }

            if (enhancement is not null)
            {
                state.SetEnhancement(unit.Id, enhancement);
            }
        }

        // A second pass, since a leader can be listed after the unit it leads (or vice versa) and AddUnit only
        // assigns each unit's real id once it has actually been added.
        foreach (var plannedUnit in saved.Units)
        {
            if (!idMap.TryGetValue(plannedUnit.Id, out var unitId))
            {
                continue;
            }

            if (plannedUnit.LeaderUnitId is { } leaderId && idMap.TryGetValue(leaderId, out var resolvedLeaderId))
            {
                state.SetLeader(unitId, resolvedLeaderId);
            }

            if (plannedUnit.IsWarlord)
            {
                state.SetWarlord(unitId);
            }
        }

        return errors.Count > 0 ? OpenSavedArmyResult.Failure(errors) : OpenSavedArmyResult.Success;
    }
}
