namespace Servitor.RulesData.Merging;

public sealed record MergeReport(
    string DataVersion,
    DateTimeOffset GeneratedAtUtc,
    MergeSummary Summary,
    IReadOnlyList<UnmatchedDatasheet> UnmatchedDatasheets,
    IReadOnlyList<AmbiguousDatasheetMatch> AmbiguousMatches,
    IReadOnlyList<UnreferencedDatasheet> UnreferencedRulesTextDatasheets,
    IReadOnlyList<UnresolvedStratagemReference> UnresolvedStratagemIds,
    IReadOnlyList<string> UnmappedCatalogues,
    IReadOnlyList<string> CoreRulesKeptFromBsData,
    IReadOnlyList<string> CoreDuplicatesDropped,
    IReadOnlyList<string> Warnings);

/// <param name="BsDataDatasheets">Every datasheet read from BSData.</param>
/// <param name="MatchedDatasheets">Datasheets with exactly one rules-text counterpart.</param>
/// <param name="AmbiguousDatasheets">Datasheets with several rules-text counterparts (their stratagem ids are unioned).</param>
/// <param name="UnmatchedDatasheets">Datasheets of a mapped catalogue with no rules-text counterpart.</param>
/// <param name="UnmappedDatasheets">Datasheets of a catalogue that has no faction mapping at all.</param>
/// <param name="DatasheetsWithStratagems">Datasheets that ended up with at least one stratagem id.</param>
/// <param name="CoreStratagems">Core stratagems taken over from the rules-text source, duplicates removed.</param>
/// <param name="CoreStratagemsOutsideStandardMode">Of those, the Boarding Actions and Challenger stratagems.</param>
/// <param name="CoreActions">Cost-free actions (Advance Move, Normal Shooting, ...) taken over as core rules, duplicates removed.</param>
/// <param name="CoreDuplicatesDropped">Core stratagems and actions dropped as older copies of another entry.</param>
/// <param name="FactionStratagems">Faction stratagems taken over from the rules-text source.</param>
/// <param name="CoreRulesFromBsData">BSData core rules, all kept untouched.</param>
/// <param name="CoreRulesAdded">Rules-text core rules that BSData has no rule of that name for.</param>
/// <param name="CoreRulesAlreadyInBsData">Rules-text core rules skipped because BSData already has a rule of that name.</param>
public sealed record MergeSummary(
    int BsDataDatasheets,
    int MatchedDatasheets,
    int AmbiguousDatasheets,
    int UnmatchedDatasheets,
    int UnmappedDatasheets,
    int DatasheetsWithStratagems,
    int CoreStratagems,
    int CoreStratagemsOutsideStandardMode,
    int CoreActions,
    int CoreDuplicatesDropped,
    int FactionStratagems,
    int CoreRulesFromBsData,
    int CoreRulesAdded,
    int CoreRulesAlreadyInBsData);

public sealed record UnmatchedDatasheet(string Catalogue, string Name);

public sealed record AmbiguousDatasheetMatch(string Catalogue, string Name, IReadOnlyList<string> Candidates);

public sealed record UnreferencedDatasheet(string Faction, string Name);

public sealed record UnresolvedStratagemReference(string Catalogue, string Datasheet, string StratagemId);

public sealed record MergeOutcome(MergedRulesData Data, MergeReport Report);

public readonly record struct MergeProgress(string Stage, int Completed, int Total);
