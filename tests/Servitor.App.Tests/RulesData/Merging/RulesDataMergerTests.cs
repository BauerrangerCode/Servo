using System.Text.Json;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.RulesData.Merging;

public class RulesDataMergerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private const string OrksCatalogue = "Orks.json";
    private const string AeldariLibraryCatalogue = "Aeldari - Aeldari Library.json";
    private const string Unaligned = "Unaligned Forces.json";

    private static string Catalogue(string name, params string[] unitNames) =>
        "{\"catalogue\":{\"name\":\"" + name + "\",\"sharedSelectionEntries\":["
        + string.Join(",", unitNames.Select((n, i) => $"{{\"name\":\"{n}\",\"id\":\"u{i}\",\"type\":\"unit\"}}"))
        + "]}}";

    private static string Faction(string slug, string name, string[] datasheets, string[]? legendsDatasheets = null, string stratagems = "") =>
        "{\"slug\":\"" + slug + "\",\"name\":\"" + name + "\",\"dataVersion\":\"2026-01-01 00:00:00\",\"datasheets\":["
        + string.Join(",", datasheets.Select((n, i) => $"{{\"id\":\"{slug}-{i}\",\"name\":\"{n}\",\"isLegends\":false}}")
            .Concat((legendsDatasheets ?? []).Select((n, i) => $"{{\"id\":\"{slug}-l{i}\",\"name\":\"{n}\",\"isLegends\":true}}")))
        + "],\"detachments\":[{\"name\":\"Blitz\",\"abilities\":[],\"enhancements\":[],\"stratagems\":[" + stratagems + "]}]}";

    private static string Stratagem(string id, string name) =>
        "{\"id\":\"" + id + "\",\"name\":\"" + name + "\",\"type\":\"Blitz Stratagem\",\"cpCost\":\"1\",\"turn\":\"Your turn\","
        + "\"phase\":\"Charge phase\",\"detachment\":\"Blitz\","
        + "\"description\":\"<b>WHEN:</b> Now.<br><br><b>TARGET:</b> <span class=\\\"kwb\\\">WAGON</span>.<br><br><b>EFFECT:</b> Boom.\"}";

    private static string DatasheetFile(string id, params string[] stratagemIds) =>
        "{\"id\":\"" + id + "\",\"stratagemIds\":[" + string.Join(",", stratagemIds.Select(s => $"\"{s}\"")) + "]}";

    private static async Task<MergeOutcome> MergeAsync(Dictionary<string, string> files)
    {
        var merger = new RulesDataMerger(new InMemoryRulesSourceReader(files), new FixedTimeProvider(FixedNow));
        return await merger.MergeAsync();
    }

    private static Dictionary<string, string> OrksSources() => new()
    {
        [$"bsdata/wh40k-11e/{OrksCatalogue}"] = Catalogue("Xenos - Orks", "Warboss", "Boyz"),
        ["factions/orks/faction.json"] = Faction("orks", "Orks", ["Warboss", "Boyz"], stratagems: Stratagem("s1", "KRUNCH")),
        ["factions/orks/datasheets/orks-0.json"] = DatasheetFile("orks-0", "s1", "missing-id"),
        ["factions/orks/datasheets/orks-1.json"] = DatasheetFile("orks-1"),
    };

    [Fact]
    public async Task Attaches_the_stratagem_ids_of_the_matching_rules_text_datasheet()
    {
        var outcome = await MergeAsync(OrksSources());

        var warboss = Assert.Single(outcome.Data.Datasheets, d => d.Name == "Warboss");
        Assert.Equal(["s1"], warboss.StratagemIds);
        Assert.Equal(2, outcome.Report.Summary.MatchedDatasheets);
    }

    [Fact]
    public async Task Drops_and_reports_a_stratagem_id_that_resolves_to_no_stratagem()
    {
        var outcome = await MergeAsync(OrksSources());

        var unresolved = Assert.Single(outcome.Report.UnresolvedStratagemIds);
        Assert.Equal("missing-id", unresolved.StratagemId);
        Assert.Equal("Warboss", unresolved.Datasheet);
    }

    [Fact]
    public async Task Matches_names_regardless_of_case_apostrophe_style_and_punctuation()
    {
        var sources = OrksSources();
        sources[$"bsdata/wh40k-11e/{OrksCatalogue}"] = Catalogue("Xenos - Orks", "WARBOSS", "Nob's Boyz");
        sources["factions/orks/faction.json"] = Faction("orks", "Orks", ["Warboss", "Nob’s Boyz"], stratagems: Stratagem("s1", "KRUNCH"));

        var outcome = await MergeAsync(sources);

        Assert.Equal(2, outcome.Report.Summary.MatchedDatasheets);
        Assert.Empty(outcome.Report.UnmatchedDatasheets);
    }

    [Fact]
    public async Task Keeps_a_datasheet_without_a_counterpart_unchanged_and_reports_it()
    {
        var sources = OrksSources();
        sources[$"bsdata/wh40k-11e/{OrksCatalogue}"] = Catalogue("Xenos - Orks", "Warboss", "Squighog Boyz");

        var outcome = await MergeAsync(sources);

        var orphan = Assert.Single(outcome.Data.Datasheets, d => d.Name == "Squighog Boyz");
        Assert.Empty(orphan.StratagemIds);
        var reported = Assert.Single(outcome.Report.UnmatchedDatasheets);
        Assert.Equal(("Orks.json", "Squighog Boyz"), (reported.Catalogue, reported.Name));
    }

    [Fact]
    public async Task A_legends_datasheet_only_matches_a_rules_text_datasheet_flagged_as_legends()
    {
        var sources = OrksSources();
        sources[$"bsdata/wh40k-11e/{OrksCatalogue}"] = Catalogue("Xenos - Orks", "Warboss [Legends]");
        sources["factions/orks/faction.json"] = Faction(
            "orks", "Orks", ["Warboss"], ["Warboss"], Stratagem("s1", "KRUNCH"));
        sources["factions/orks/datasheets/orks-l0.json"] = DatasheetFile("orks-l0");

        var outcome = await MergeAsync(sources);

        var legends = Assert.Single(outcome.Data.Datasheets);
        Assert.Empty(legends.StratagemIds);
        Assert.Equal(1, outcome.Report.Summary.MatchedDatasheets);
    }

    [Fact]
    public async Task Unions_the_stratagem_ids_when_a_shared_library_datasheet_matches_several_factions()
    {
        var sources = new Dictionary<string, string>
        {
            [$"bsdata/wh40k-11e/{AeldariLibraryCatalogue}"] = Catalogue("Aeldari - Aeldari Library", "Troupe"),
            ["factions/aeldari/faction.json"] = Faction("aeldari", "Aeldari", ["Troupe"], stratagems: Stratagem("a1", "A")),
            ["factions/aeldari/datasheets/aeldari-0.json"] = DatasheetFile("aeldari-0", "a1"),
            ["factions/drukhari/faction.json"] = Faction("drukhari", "Drukhari", ["Troupe"], stratagems: Stratagem("d1", "D")),
            ["factions/drukhari/datasheets/drukhari-0.json"] = DatasheetFile("drukhari-0", "d1", "a1"),
        };

        var outcome = await MergeAsync(sources);

        var troupe = Assert.Single(outcome.Data.Datasheets);
        Assert.Equal(["a1", "d1"], troupe.StratagemIds);
        var ambiguous = Assert.Single(outcome.Report.AmbiguousMatches);
        Assert.Equal(2, ambiguous.Candidates.Count);
    }

    [Fact]
    public async Task A_catalogue_without_faction_mapping_is_kept_and_reported()
    {
        var sources = new Dictionary<string, string>
        {
            [$"bsdata/wh40k-11e/{Unaligned}"] = Catalogue("Unaligned Forces", "Rogue Trader"),
        };

        var outcome = await MergeAsync(sources);

        Assert.Single(outcome.Data.Datasheets);
        Assert.Equal([Unaligned], outcome.Report.UnmappedCatalogues);
        Assert.Equal(1, outcome.Report.Summary.UnmappedDatasheets);
    }

    [Fact]
    public async Task Takes_over_core_and_faction_stratagems_with_structured_fields()
    {
        var sources = OrksSources();
        sources["core/core-stratagems.json"] =
            "[{\"id\":\"c1\",\"name\":\"REROLL\",\"type\":\"Core Stratagem\",\"cpCost\":\"1\",\"description\":\"<b>EFFECT:</b> Re-roll.\"}]";

        var outcome = await MergeAsync(sources);

        var core = Assert.Single(outcome.Data.CoreStratagems);
        Assert.Equal("Re-roll.", core.Effect);
        var orks = Assert.Single(outcome.Data.Factions, f => f.FactionId == "orks");
        var krunch = Assert.Single(orks.Stratagems);
        Assert.Equal(("Now.", "**WAGON**.", "Boom."), (krunch.When, krunch.Target, krunch.Effect));
        Assert.Equal((1, 1), (outcome.Report.Summary.CoreStratagems, outcome.Report.Summary.FactionStratagems));
    }

    private static string CoreEntry(string id, string name, string type, string cpCost) =>
        $"{{\"id\":\"{id}\",\"name\":\"{name}\",\"type\":\"{type}\",\"cpCost\":\"{cpCost}\",\"description\":\"<b>EFFECT:</b> Text {id}.\"}}";

    [Fact]
    public async Task Keeps_the_newest_of_duplicate_core_stratagems_per_game_mode_and_reports_the_rest()
    {
        var sources = OrksSources();
        sources["core/core-stratagems.json"] = "[" + string.Join(",", [
            CoreEntry("000008335002", "COMMAND RE-ROLL", "Core – Battle Tactic Stratagem", "1"),
            CoreEntry("000010810002", "COMMAND RE-ROLL", "Core Stratagem", "1"),
            CoreEntry("000009218002", "COMMAND RE-ROLL", "Boarding Actions – Epic Deed Stratagem", "1"),
            CoreEntry("000008335003", "COUNTER-OFFENSIVE", "Core – Strategic Ploy Stratagem", "2"),
            CoreEntry("000010810012", "COUNTEROFFENSIVE", "Core Stratagem", "2"),
            CoreEntry("000008335007", "TANK SHOCK", "Core – Strategic Ploy Stratagem", "1"),
            CoreEntry("000010254002", "ALL IN", "Challenger – Strategic Ploy Stratagem", "0"),
        ]) + "]";

        var outcome = await MergeAsync(sources);

        Assert.Equal(
            ["000010810002", "000009218002", "000010810012", "000008335007", "000010254002"],
            outcome.Data.CoreStratagems.Select(s => s.Id));
        Assert.Equal(
            [GameMode.Standard, GameMode.BoardingActions, GameMode.Standard, GameMode.Standard, GameMode.Challenger],
            outcome.Data.CoreStratagems.Select(s => s.Mode));
        Assert.Equal(
            ["Stratagem COMMAND RE-ROLL (000008335002, Standard) superseded by 000010810002",
             "Stratagem COUNTER-OFFENSIVE (000008335003, Standard) superseded by 000010810012"],
            outcome.Report.CoreDuplicatesDropped);
        Assert.Equal((5, 2, 2), (
            outcome.Report.Summary.CoreStratagems,
            outcome.Report.Summary.CoreStratagemsOutsideStandardMode,
            outcome.Report.Summary.CoreDuplicatesDropped));
    }

    [Fact]
    public async Task Takes_cost_free_core_entries_over_as_action_rules_not_as_stratagems()
    {
        var sources = OrksSources();
        sources["core/core-stratagems.json"] = "[" + string.Join(",", [
            CoreEntry("000010811004", "ADVANCE MOVE", "Movement Ability", ""),
            CoreEntry("000010851002", "ADVANCE MOVE", "Movement Ability", ""),
            CoreEntry("000010812004", "CLOSE\u2011QUARTERS SHOOTING", "Shooting Ability", ""),
            CoreEntry("000010810002", "COMMAND RE-ROLL", "Core Stratagem", "1"),
        ]) + "]";

        var outcome = await MergeAsync(sources);

        Assert.Equal(["COMMAND RE-ROLL"], outcome.Data.CoreStratagems.Select(s => s.Name));
        var actions = outcome.Data.CoreRules.Where(r => r.Kind == CoreRuleKind.Action).ToList();
        Assert.Equal(["Advance Move", "Close-Quarters Shooting"], actions.Select(a => a.Name));
        Assert.Equal("000010851002", actions[0].Id);
        Assert.Equal(2, outcome.Report.Summary.CoreActions);
        Assert.Equal(["Action Advance Move (000010811004) superseded by 000010851002"], outcome.Report.CoreDuplicatesDropped);
    }

    [Fact]
    public async Task Keeps_bsdata_core_rules_untouched_and_adds_only_the_missing_ones()
    {
        var sources = new Dictionary<string, string>
        {
            [$"bsdata/wh40k-11e/{RulesSourceManifest.BsDataGameSystemFileName}"] =
                "{\"gameSystem\":{\"sharedRules\":[{\"id\":\"g1\",\"name\":\"Torrent\",\"description\":\"BSData text.\"}]}}",
            ["core/core-abilities.json"] =
                "[{\"id\":\"r1\",\"number\":null,\"name\":\"[TORRENT]\",\"description\":\"<p>Rules-text text.</p>\"}]",
            ["core/moving.json"] =
                "[{\"id\":\"03.moving\",\"number\":\"03\",\"name\":\"Moving\",\"description\":\"<p>Move <b>models</b>.</p>\"}]",
        };

        var outcome = await MergeAsync(sources);

        Assert.Equal(["Torrent", "Moving"], outcome.Data.CoreRules.Select(r => r.Name));
        Assert.Equal("BSData text.", outcome.Data.CoreRules[0].Markdown);
        Assert.Equal(CoreRuleSource.RulesText, outcome.Data.CoreRules[1].Source);
        Assert.Equal("Move **models**.", outcome.Data.CoreRules[1].Markdown);
        Assert.Equal(["[TORRENT]"], outcome.Report.CoreRulesKeptFromBsData);
        Assert.Equal((1, 1, 1), (
            outcome.Report.Summary.CoreRulesFromBsData,
            outcome.Report.Summary.CoreRulesAdded,
            outcome.Report.Summary.CoreRulesAlreadyInBsData));
    }

    [Fact]
    public async Task Builds_the_data_version_from_both_sources_and_stamps_the_generation_time()
    {
        var outcome = await MergeAsync(OrksSources());

        Assert.Equal($"{RulesSourceManifest.BsDataVersion}+2026-01-01 00:00:00", outcome.Data.DataVersion);
        Assert.Equal(FixedNow, outcome.Data.GeneratedAtUtc);
        Assert.Equal(outcome.Data.DataVersion, outcome.Report.DataVersion);
    }

    [Fact]
    public async Task Missing_source_files_become_warnings_instead_of_exceptions()
    {
        var outcome = await MergeAsync([]);

        Assert.Empty(outcome.Data.Datasheets);
        Assert.Contains(outcome.Report.Warnings, w => w.Contains("Source file missing", StringComparison.Ordinal));
        Assert.Equal(RulesSourceManifest.BsDataVersion, outcome.Data.DataVersion);
    }

    [Fact]
    public async Task A_datasheet_file_that_is_not_json_becomes_a_warning()
    {
        var sources = OrksSources();
        sources["factions/orks/datasheets/orks-1.json"] = "<html>not json</html>";

        var outcome = await MergeAsync(sources);

        Assert.Contains(outcome.Report.Warnings, w => w.Contains("orks-1.json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reports_progress_for_every_stage()
    {
        var reports = new System.Collections.Concurrent.ConcurrentQueue<MergeProgress>();
        var merger = new RulesDataMerger(new InMemoryRulesSourceReader(OrksSources()), new FixedTimeProvider(FixedNow));

        await merger.MergeAsync(new SynchronousProgress<MergeProgress>(reports.Enqueue));

        var stages = reports.Select(r => r.Stage).Distinct().ToList();
        Assert.Equal(
            ["BSData catalogues", "Core rules", "Factions", "Datasheet stratagem links"],
            stages);
        Assert.All(reports, r => Assert.InRange(r.Completed, 1, r.Total));
    }

    [Fact]
    public async Task Merges_the_real_data_without_warnings_and_round_trips_through_json()
    {
        var merger = new RulesDataMerger(new DirectoryRulesSourceReader(TestPaths.WwwrootDataDirectory), TimeProvider.System);

        var outcome = await merger.MergeAsync();

        Assert.Empty(outcome.Report.Warnings);
        Assert.Equal(1519, outcome.Data.Datasheets.Count);
        Assert.Equal(28, outcome.Data.CoreStratagems.Count);
        Assert.Equal(10, outcome.Data.CoreStratagems.Count(s => s.Mode == GameMode.Standard && s.Id.StartsWith("0000108")));
        Assert.Equal(22, outcome.Data.CoreRules.Count(r => r.Kind == CoreRuleKind.Action));
        Assert.Equal(18, outcome.Report.CoreDuplicatesDropped.Count);
        Assert.Equal(1564, outcome.Data.Factions.Sum(f => f.Stratagems.Count));

        var knownIds = outcome.Data.CoreStratagems.Concat(outcome.Data.Factions.SelectMany(f => f.Stratagems))
            .Select(s => s.Id).ToHashSet();
        Assert.All(outcome.Data.Datasheets.SelectMany(d => d.StratagemIds), id => Assert.Contains(id, knownIds));

        var warboss = Assert.Single(outcome.Data.Datasheets, d => d.Name == "Warboss" && d.Faction.Contains("Orks"));
        Assert.NotEmpty(warboss.StratagemIds);

        var json = JsonSerializer.Serialize(outcome.Data, MergedDataJson.Options);
        var reloaded = JsonSerializer.Deserialize<MergedRulesData>(json, MergedDataJson.Options);
        Assert.NotNull(reloaded);
        Assert.Equal(json, JsonSerializer.Serialize(reloaded, MergedDataJson.Options));
    }

    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
