using System.Collections.Concurrent;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Mapping;

namespace Servitor.RulesData.Merging;

/// <summary>
/// Builds the merged single source of truth from the raw sources without modifying them. BSData is authoritative:
/// its datasheets and core rules pass through unchanged. From the rules-text source only three things are added: the
/// stratagem ids per datasheet (joined by faction and name), all stratagems, and the core rules BSData lacks.
/// </summary>
public sealed class RulesDataMerger(IRulesSourceReader reader, TimeProvider timeProvider)
{
    private const int DatasheetReadParallelism = 8;

    private const string StageCatalogues = "BSData catalogues";
    private const string StageCoreRules = "Core rules";
    private const string StageFactions = "Factions";
    private const string StageDatasheetLinks = "Datasheet stratagem links";

    private sealed record LoadedFaction(string Slug, FactionRulesTextMappingResult Mapping);

    private sealed record RulesTextDatasheet(string FactionSlug, DatasheetReference Reference);

    public async Task<MergeOutcome> MergeAsync(
        IProgress<MergeProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();

        var catalogues = await LoadCataloguesAsync(progress, warnings, cancellationToken);
        var bsDataRules = await LoadBsDataCoreRulesAsync(warnings, cancellationToken);
        var coreFile = await LoadCoreStratagemsFileAsync(warnings, cancellationToken);
        var rulesTextCoreRules = await LoadRulesTextCoreRulesAsync(progress, warnings, cancellationToken);
        var factions = await LoadFactionsAsync(progress, warnings, cancellationToken);
        var stratagemLinks = await LoadStratagemLinksAsync(factions, progress, warnings, cancellationToken);

        var coreStratagems = CoreDuplicateConsolidator.Stratagems(coreFile.Stratagems);
        var coreActions = CoreDuplicateConsolidator.Actions(coreFile.Actions);

        var knownStratagemIds = coreStratagems.Kept
            .Concat(factions.SelectMany(f => f.Mapping.Stratagems))
            .Select(s => s.Id)
            .ToHashSet();

        var joined = JoinDatasheets(catalogues, factions, stratagemLinks, knownStratagemIds);
        var (proseRules, keptFromBsData) = MergeCoreRules(bsDataRules, rulesTextCoreRules);
        var coreRules = proseRules.Concat(coreActions.Kept).ToList();

        var generatedAt = timeProvider.GetUtcNow();
        var dataVersion = BuildDataVersion(factions);
        var mergedFactions = factions
            .Select(f => new MergedFactionRules(
                f.Slug, f.Mapping.Identity.Name, f.Mapping.Stratagems, f.Mapping.Abilities, f.Mapping.Enhancements))
            .ToList();

        var data = new MergedRulesData(dataVersion, generatedAt, joined.Datasheets, coreStratagems.Kept, mergedFactions, coreRules);
        var summary = new MergeSummary(
            joined.Datasheets.Count,
            joined.MatchedCount,
            joined.Ambiguous.Count,
            joined.Unmatched.Count,
            joined.UnmappedCount,
            joined.Datasheets.Count(d => d.StratagemIds.Count > 0),
            coreStratagems.Kept.Count,
            coreStratagems.Kept.Count(s => s.Mode != GameMode.Standard),
            coreActions.Kept.Count,
            coreStratagems.Dropped.Count + coreActions.Dropped.Count,
            factions.Sum(f => f.Mapping.Stratagems.Count),
            bsDataRules.Count,
            proseRules.Count - bsDataRules.Count,
            keptFromBsData.Count);

        var report = new MergeReport(
            dataVersion,
            generatedAt,
            summary,
            joined.Unmatched,
            joined.Ambiguous,
            joined.Unreferenced,
            joined.Unresolved,
            joined.UnmappedCatalogues,
            keptFromBsData,
            coreStratagems.Dropped.Concat(coreActions.Dropped).ToList(),
            warnings);

        return new MergeOutcome(data, report);
    }

    private async Task<IReadOnlyList<(string FileName, IReadOnlyList<Datasheet> Datasheets)>> LoadCataloguesAsync(
        IProgress<MergeProgress>? progress, List<string> warnings, CancellationToken cancellationToken)
    {
        var fileNames = RulesSourceManifest.BsDataCatalogueFileNames;
        var loaded = new List<(string, IReadOnlyList<Datasheet>)>();

        for (var i = 0; i < fileNames.Count; i++)
        {
            var path = $"{RulesSourceManifest.BsDataDirectory}/{fileNames[i]}";
            var json = await ReadOrWarnAsync(path, warnings, cancellationToken);
            if (json is not null)
            {
                var result = BsDataCatalogueMapper.Map(json);
                warnings.AddRange(result.Warnings.Select(w => $"{path}: {w}"));
                loaded.Add((fileNames[i], result.Datasheets));
            }

            progress?.Report(new MergeProgress(StageCatalogues, i + 1, fileNames.Count));
        }

        return loaded;
    }

    private async Task<IReadOnlyList<CoreRule>> LoadBsDataCoreRulesAsync(
        List<string> warnings, CancellationToken cancellationToken)
    {
        var path = $"{RulesSourceManifest.BsDataDirectory}/{RulesSourceManifest.BsDataGameSystemFileName}";
        var json = await ReadOrWarnAsync(path, warnings, cancellationToken);
        if (json is null)
        {
            return [];
        }

        var result = BsDataGameSystemMapper.Map(json);
        warnings.AddRange(result.Warnings.Select(w => $"{path}: {w}"));
        return result.Rules;
    }

    private async Task<CoreStratagemsMappingResult> LoadCoreStratagemsFileAsync(
        List<string> warnings, CancellationToken cancellationToken)
    {
        var path = $"{RulesSourceManifest.CoreDirectory}/{RulesSourceManifest.CoreStratagemsFileName}";
        var json = await ReadOrWarnAsync(path, warnings, cancellationToken);
        if (json is null)
        {
            return new CoreStratagemsMappingResult([], [], []);
        }

        var result = RulesTextMapper.MapCoreStratagems(json);
        warnings.AddRange(result.Warnings.Select(w => $"{path}: {w}"));
        return result;
    }

    private async Task<IReadOnlyList<CoreRule>> LoadRulesTextCoreRulesAsync(
        IProgress<MergeProgress>? progress, List<string> warnings, CancellationToken cancellationToken)
    {
        var fileNames = RulesSourceManifest.CoreRuleFileNames;
        var rules = new List<CoreRule>();

        for (var i = 0; i < fileNames.Count; i++)
        {
            var path = $"{RulesSourceManifest.CoreDirectory}/{fileNames[i]}";
            var json = await ReadOrWarnAsync(path, warnings, cancellationToken);
            if (json is not null)
            {
                var result = RulesTextMapper.MapCoreRules(json);
                warnings.AddRange(result.Warnings.Select(w => $"{path}: {w}"));
                rules.AddRange(result.Rules);
            }

            progress?.Report(new MergeProgress(StageCoreRules, i + 1, fileNames.Count));
        }

        return rules;
    }

    private async Task<IReadOnlyList<LoadedFaction>> LoadFactionsAsync(
        IProgress<MergeProgress>? progress, List<string> warnings, CancellationToken cancellationToken)
    {
        var slugs = RulesSourceManifest.FactionSlugs;
        var loaded = new List<LoadedFaction>();

        for (var i = 0; i < slugs.Count; i++)
        {
            var path = $"{RulesSourceManifest.FactionsDirectory}/{slugs[i]}/{RulesSourceManifest.FactionFileName}";
            var json = await ReadOrWarnAsync(path, warnings, cancellationToken);
            if (json is not null)
            {
                var mapping = RulesTextMapper.MapFaction(json);
                warnings.AddRange(mapping.Warnings.Select(w => $"{path}: {w}"));
                loaded.Add(new LoadedFaction(slugs[i], mapping));
            }

            progress?.Report(new MergeProgress(StageFactions, i + 1, slugs.Count));
        }

        return loaded;
    }

    private async Task<IReadOnlyDictionary<(string FactionSlug, string DatasheetId), IReadOnlyList<string>>>
        LoadStratagemLinksAsync(
            IReadOnlyList<LoadedFaction> factions,
            IProgress<MergeProgress>? progress,
            List<string> warnings,
            CancellationToken cancellationToken)
    {
        var requests = factions
            .SelectMany(f => f.Mapping.Datasheets.Select(d => (f.Slug, d.Id)))
            .ToList();
        var links = new ConcurrentDictionary<(string, string), IReadOnlyList<string>>();
        var concurrentWarnings = new ConcurrentBag<string>();
        var completed = 0;

        await Parallel.ForEachAsync(
            requests,
            new ParallelOptions { MaxDegreeOfParallelism = DatasheetReadParallelism, CancellationToken = cancellationToken },
            async (request, token) =>
            {
                var path = $"{RulesSourceManifest.FactionsDirectory}/{request.Slug}/datasheets/{request.Id}.json";
                var json = await reader.ReadTextAsync(path, token);
                if (json is null)
                {
                    concurrentWarnings.Add($"Source file missing: {path}");
                }
                else if (RulesTextMapper.MapDatasheetStratagemLinks(json) is { } parsed)
                {
                    links[(request.Slug, request.Id)] = parsed.StratagemIds;
                }
                else
                {
                    concurrentWarnings.Add($"{path}: not a readable datasheet file.");
                }

                progress?.Report(new MergeProgress(
                    StageDatasheetLinks, Interlocked.Increment(ref completed), requests.Count));
            });

        warnings.AddRange(concurrentWarnings.Order(StringComparer.Ordinal));
        return links;
    }

    private static JoinResult JoinDatasheets(
        IReadOnlyList<(string FileName, IReadOnlyList<Datasheet> Datasheets)> catalogues,
        IReadOnlyList<LoadedFaction> factions,
        IReadOnlyDictionary<(string FactionSlug, string DatasheetId), IReadOnlyList<string>> stratagemLinks,
        IReadOnlySet<string> knownStratagemIds)
    {
        var index = new Dictionary<(string Slug, string Key, bool IsLegends), List<RulesTextDatasheet>>();
        foreach (var faction in factions)
        {
            foreach (var reference in faction.Mapping.Datasheets)
            {
                var key = (faction.Slug, NameNormalizer.Normalize(reference.Name), reference.IsLegends);
                if (!index.TryGetValue(key, out var bucket))
                {
                    index[key] = bucket = [];
                }

                bucket.Add(new RulesTextDatasheet(faction.Slug, reference));
            }
        }

        var datasheets = new List<Datasheet>();
        var unmatched = new List<UnmatchedDatasheet>();
        var ambiguous = new List<AmbiguousDatasheetMatch>();
        var unresolved = new List<UnresolvedStratagemReference>();
        var unmappedCatalogues = new List<string>();
        var referenced = new HashSet<(string Slug, string Id)>();
        var matchedCount = 0;
        var unmappedCount = 0;

        foreach (var (fileName, sheets) in catalogues)
        {
            var slugs = FactionCatalogueMap.FactionSlugsFor(fileName);
            if (slugs.Count == 0)
            {
                if (sheets.Count > 0)
                {
                    unmappedCatalogues.Add(fileName);
                    unmappedCount += sheets.Count;
                }

                datasheets.AddRange(sheets);
                continue;
            }

            foreach (var sheet in sheets)
            {
                var name = NameNormalizer.Normalize(sheet.Name);
                var isLegends = NameNormalizer.IsLegends(sheet.Name);
                var hits = slugs
                    .SelectMany(slug => index.GetValueOrDefault((slug, name, isLegends)) ?? [])
                    .ToList();

                if (hits.Count == 0)
                {
                    unmatched.Add(new UnmatchedDatasheet(fileName, sheet.Name));
                    datasheets.Add(sheet);
                    continue;
                }

                if (hits.Count == 1)
                {
                    matchedCount++;
                }
                else
                {
                    ambiguous.Add(new AmbiguousDatasheetMatch(
                        fileName, sheet.Name, hits.Select(h => $"{h.FactionSlug}: {h.Reference.Name}").ToList()));
                }

                var stratagemIds = new List<string>();
                foreach (var hit in hits)
                {
                    referenced.Add((hit.FactionSlug, hit.Reference.Id));
                    foreach (var id in stratagemLinks.GetValueOrDefault((hit.FactionSlug, hit.Reference.Id)) ?? [])
                    {
                        if (knownStratagemIds.Contains(id))
                        {
                            if (!stratagemIds.Contains(id))
                            {
                                stratagemIds.Add(id);
                            }
                        }
                        else
                        {
                            unresolved.Add(new UnresolvedStratagemReference(fileName, sheet.Name, id));
                        }
                    }
                }

                datasheets.Add(sheet with { StratagemIds = stratagemIds });
            }
        }

        var unreferenced = factions
            .SelectMany(f => f.Mapping.Datasheets.Select(d => (f.Slug, d)))
            .Where(x => !referenced.Contains((x.Slug, x.d.Id)))
            .Select(x => new UnreferencedDatasheet(x.Slug, x.d.Name))
            .ToList();

        return new JoinResult(datasheets, matchedCount, unmappedCount, unmatched, ambiguous, unreferenced, unresolved, unmappedCatalogues);
    }

    private static (IReadOnlyList<CoreRule> Rules, IReadOnlyList<string> KeptFromBsData) MergeCoreRules(
        IReadOnlyList<CoreRule> bsDataRules, IReadOnlyList<CoreRule> rulesTextRules)
    {
        var bsDataNames = bsDataRules.Select(r => NameNormalizer.Normalize(r.Name)).ToHashSet();
        var merged = new List<CoreRule>(bsDataRules);
        var keptFromBsData = new List<string>();
        var addedIds = new HashSet<string>();

        foreach (var rule in rulesTextRules)
        {
            if (bsDataNames.Contains(NameNormalizer.Normalize(rule.Name)))
            {
                keptFromBsData.Add(rule.Name);
            }
            else if (addedIds.Add(rule.Id))
            {
                merged.Add(rule);
            }
        }

        return (merged, keptFromBsData);
    }

    private static string BuildDataVersion(IReadOnlyList<LoadedFaction> factions)
    {
        var newestFactionVersion = factions
            .Select(f => f.Mapping.Identity.DataVersion)
            .Where(v => v.Length > 0)
            .Order(StringComparer.Ordinal)
            .LastOrDefault();

        return newestFactionVersion is null
            ? RulesSourceManifest.BsDataVersion
            : $"{RulesSourceManifest.BsDataVersion}+{newestFactionVersion}";
    }

    private async Task<string?> ReadOrWarnAsync(string path, List<string> warnings, CancellationToken cancellationToken)
    {
        var text = await reader.ReadTextAsync(path, cancellationToken);
        if (text is null)
        {
            warnings.Add($"Source file missing: {path}");
        }

        return text;
    }

    private sealed record JoinResult(
        IReadOnlyList<Datasheet> Datasheets,
        int MatchedCount,
        int UnmappedCount,
        IReadOnlyList<UnmatchedDatasheet> Unmatched,
        IReadOnlyList<AmbiguousDatasheetMatch> Ambiguous,
        IReadOnlyList<UnreferencedDatasheet> Unreferenced,
        IReadOnlyList<UnresolvedStratagemReference> Unresolved,
        IReadOnlyList<string> UnmappedCatalogues);
}
