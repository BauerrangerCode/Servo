using System.Globalization;
using System.Text;

namespace Servitor.RulesData.Merging;

public static class MergeReportFormatter
{
    public static string ToMarkdown(MergeReport report)
    {
        var text = new StringBuilder();
        var summary = report.Summary;

        text.AppendLine("# Rules data merge report").AppendLine();
        text.AppendLine($"- Data version: `{report.DataVersion}`");
        text.AppendLine($"- Generated (UTC): {report.GeneratedAtUtc.ToString("u", CultureInfo.InvariantCulture)}");
        text.AppendLine();

        text.AppendLine("## Summary").AppendLine();
        text.AppendLine("| Metric | Count |").AppendLine("| --- | ---: |");
        AppendRow(text, "BSData datasheets", summary.BsDataDatasheets);
        AppendRow(text, "Matched to exactly one rules-text datasheet", summary.MatchedDatasheets);
        AppendRow(text, "Matched to several rules-text datasheets (ids unioned)", summary.AmbiguousDatasheets);
        AppendRow(text, "No rules-text counterpart", summary.UnmatchedDatasheets);
        AppendRow(text, "In a catalogue without faction mapping", summary.UnmappedDatasheets);
        AppendRow(text, "Datasheets with at least one stratagem id", summary.DatasheetsWithStratagems);
        AppendRow(text, "Core stratagems taken over", summary.CoreStratagems);
        AppendRow(text, "  of which Boarding Actions / Challenger", summary.CoreStratagemsOutsideStandardMode);
        AppendRow(text, "Core actions taken over (no CP)", summary.CoreActions);
        AppendRow(text, "Core stratagems and actions dropped as older duplicates", summary.CoreDuplicatesDropped);
        AppendRow(text, "Faction stratagems taken over", summary.FactionStratagems);
        AppendRow(text, "Core rules from BSData (untouched)", summary.CoreRulesFromBsData);
        AppendRow(text, "Core rules added from rules-text source", summary.CoreRulesAdded);
        AppendRow(text, "Core rules skipped, BSData already has them", summary.CoreRulesAlreadyInBsData);
        text.AppendLine();

        AppendSection(text, "Warnings", report.Warnings, w => w);
        AppendSection(text, "Catalogues without faction mapping", report.UnmappedCatalogues, c => c);
        AppendSection(text, "BSData datasheets without rules-text counterpart", report.UnmatchedDatasheets, u => $"{u.Catalogue}: {u.Name}");
        AppendSection(
            text,
            "Ambiguous matches (stratagem ids of all candidates unioned)",
            report.AmbiguousMatches,
            a => $"{a.Catalogue}: {a.Name} — {string.Join("; ", a.Candidates)}");
        AppendSection(text, "Unresolved stratagem ids (dropped)", report.UnresolvedStratagemIds, u => $"{u.Catalogue}: {u.Datasheet} — {u.StratagemId}");
        AppendSection(text, "Rules-text datasheets no BSData datasheet points at", report.UnreferencedRulesTextDatasheets, u => $"{u.Faction}: {u.Name}");
        AppendSection(text, "Core duplicates dropped (newest id kept)", report.CoreDuplicatesDropped, d => d);
        AppendSection(text, "Rules-text core rules skipped (BSData already has them)", report.CoreRulesKeptFromBsData, r => r);

        return text.ToString();
    }

    private static void AppendRow(StringBuilder text, string label, int count) =>
        text.AppendLine($"| {label} | {count.ToString(CultureInfo.InvariantCulture)} |");

    private static void AppendSection<T>(StringBuilder text, string title, IReadOnlyList<T> items, Func<T, string> describe)
    {
        if (items.Count == 0)
        {
            return;
        }

        text.AppendLine($"## {title} ({items.Count.ToString(CultureInfo.InvariantCulture)})").AppendLine();
        foreach (var item in items)
        {
            text.AppendLine($"- {describe(item)}");
        }

        text.AppendLine();
    }
}
