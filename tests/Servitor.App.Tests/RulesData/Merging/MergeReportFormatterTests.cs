using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.RulesData.Merging;

public class MergeReportFormatterTests
{
    private static MergeReport Report(
        IReadOnlyList<UnmatchedDatasheet>? unmatched = null,
        IReadOnlyList<string>? warnings = null) => new(
        "v1",
        new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero),
        new MergeSummary(1519, 1309, 60, 127, 23, 1369, 28, 14, 22, 18, 1564, 49, 164, 1),
        unmatched ?? [],
        [],
        [],
        [],
        [],
        [],
        [],
        warnings ?? []);

    [Fact]
    public void Lists_the_data_version_and_every_summary_count()
    {
        var markdown = MergeReportFormatter.ToMarkdown(Report());

        Assert.Contains("`v1`", markdown);
        Assert.Contains("| BSData datasheets | 1519 |", markdown);
        Assert.Contains("| Faction stratagems taken over | 1564 |", markdown);
        Assert.Contains("| Core rules added from rules-text source | 164 |", markdown);
    }

    [Fact]
    public void Lists_the_core_stratagem_action_and_duplicate_counts()
    {
        var markdown = MergeReportFormatter.ToMarkdown(Report());

        Assert.Contains("| Core stratagems taken over | 28 |", markdown);
        Assert.Contains("| Core actions taken over (no CP) | 22 |", markdown);
        Assert.Contains("| Core stratagems and actions dropped as older duplicates | 18 |", markdown);
    }

    [Fact]
    public void Renders_a_section_with_its_count_only_when_it_has_entries()
    {
        var markdown = MergeReportFormatter.ToMarkdown(
            Report(unmatched: [new UnmatchedDatasheet("Orks.json", "Squighog Boyz")]));

        Assert.Contains("## BSData datasheets without rules-text counterpart (1)", markdown);
        Assert.Contains("- Orks.json: Squighog Boyz", markdown);
        Assert.DoesNotContain("## Warnings", markdown);
    }

    [Fact]
    public void Renders_warnings_as_a_section()
    {
        var markdown = MergeReportFormatter.ToMarkdown(Report(warnings: ["Source file missing: core/moving.json"]));

        Assert.Contains("## Warnings (1)", markdown);
        Assert.Contains("- Source file missing: core/moving.json", markdown);
    }
}
