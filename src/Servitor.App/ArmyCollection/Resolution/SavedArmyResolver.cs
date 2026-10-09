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
    /// <summary>Checks whether a saved army would still resolve, without touching any <see cref="IArmyBuilderState"/>.</summary>
    public static async Task<IReadOnlyList<string>> ValidateAsync(
        SavedArmy saved,
        IMergedRulesDataProvider rulesDataProvider,
        IDatasheetCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        var plan = await PlanAsync(saved, rulesDataProvider, catalog, cancellationToken);
        return plan.Errors;
    }

    public static async Task<OpenSavedArmyResult> ResolveAsync(
        SavedArmy saved,
        IArmyBuilderState state,
        IMergedRulesDataProvider rulesDataProvider,
        IDatasheetCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        var plan = await PlanAsync(saved, rulesDataProvider, catalog, cancellationToken);
        if (plan.Errors.Count > 0)
        {
            return OpenSavedArmyResult.Failure(plan.Errors);
        }

        var errors = new List<string>();

        // Reset first: IArmyBuilderState is shared across Import/Build/Play, so it may already hold this exact
        // faction (e.g. from an earlier open of this same saved army, or from Build) — SelectFaction alone is a
        // no-op for units/detachments when the faction doesn't change, which would otherwise make every add below
        // fail as AlreadySelected/AtLimit against the leftover army instead of the one being reopened.
        state.Reset();
        state.SelectFaction(saved.FactionId, saved.AllyFactionIds);
        state.SelectBattleSize(plan.BattleSize!);

        foreach (var detachment in plan.Detachments)
        {
            var result = state.AddDetachment(detachment);
            if (result != DetachmentAddResult.Added)
            {
                errors.Add($"Could not add detachment \"{detachment.Name}\": {result}.");
            }
        }

        var idMap = new Dictionary<Guid, Guid>();
        foreach (var (plannedUnit, datasheet, size, enhancement) in plan.UnitPlans)
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

            if (plannedUnit.DeclaredWeapons is { } declared)
            {
                state.SetDeclaredWeapons(unit.Id, declared);
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

    /// <summary>The read-only half of resolving a saved army: everything that only reads the catalog, shared by
    /// <see cref="ValidateAsync"/> (which stops here) and <see cref="ResolveAsync"/> (which then applies the plan).</summary>
    private static async Task<Plan> PlanAsync(
        SavedArmy saved, IMergedRulesDataProvider rulesDataProvider, IDatasheetCatalog catalog, CancellationToken cancellationToken)
    {
        var rulesData = await rulesDataProvider.GetAsync(cancellationToken);
        if (rulesData is null)
        {
            return Plan.Failed(["No rules data is available yet — use \"Merge data\" in the top bar first."]);
        }

        var faction = rulesData.Factions.FirstOrDefault(f => f.FactionId == saved.FactionId);
        if (faction is null)
        {
            return Plan.Failed([$"Faction \"{saved.FactionId}\" is no longer in the rules data."]);
        }

        var battleSize = BattleSizes.All.FirstOrDefault(b => b.Name == saved.BattleSizeName);
        if (battleSize is null)
        {
            return Plan.Failed([$"Unknown battle size \"{saved.BattleSizeName}\"."]);
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

        return new Plan(errors, battleSize, detachments, unitPlans);
    }

    private sealed record Plan(
        List<string> Errors,
        BattleSize? BattleSize,
        List<Detachment> Detachments,
        List<(SavedUnit Saved, Datasheet Datasheet, UnitSize Size, Enhancement? Enhancement)> UnitPlans)
    {
        public static Plan Failed(List<string> errors) => new(errors, null, [], []);
    }
}
