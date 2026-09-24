using System.Text.Json.Nodes;
using Servitor.DataUpdate.Wahapedia;

namespace Servitor.App.Tests.DataUpdate;

public class DepotDataGeneratorTests
{
    private static readonly GeneratedData Data = DepotDataGenerator.Generate(SampleWahapediaData.Parsed());

    private static GeneratedFaction Faction => Data.Factions.Single(f => f.Slug == "faction-00");

    private static JsonObject Datasheet(string id) => Faction.Datasheets.Single(d => d.DatasheetId == id).Datasheet;

    [Fact]
    public void Every_faction_with_datasheets_is_generated_and_virtual_datasheets_are_left_out()
    {
        Assert.Equal(12, Data.Factions.Count);
        Assert.Equal(["D00", "D98"], Faction.Datasheets.Select(d => d.DatasheetId).Order());
        Assert.Equal("2026-09-23 10:12:23", Data.DataVersion);
    }

    [Fact]
    public void A_faction_without_datasheets_is_skipped()
    {
        var tables = SampleWahapediaData.Tables();
        tables["Factions.csv"] += "EMPTY|Empty Faction|https://example/e|\r\n";

        var data = DepotDataGenerator.Generate(WahapediaTables.Parse(tables));

        Assert.DoesNotContain(data.Factions, f => f.Slug == "empty-faction");
    }

    [Fact]
    public void The_manifest_lists_datasheet_summaries_sorted_by_name_with_role_points_and_flags()
    {
        var manifest = Faction.Manifest;
        Assert.Equal(["id", "slug", "name", "link", "datasheets", "detachments", "dataVersion", "datasheetCount", "detachmentCount"], manifest.Select(p => p.Key));
        Assert.Equal(2, manifest["datasheetCount"]!.GetValue<int>());

        var summaries = manifest["datasheets"]!.AsArray();
        Assert.Equal(["Boyz", "Old Warboss"], summaries.Select(s => s!["name"]!.GetValue<string>()));

        var boyz = summaries[0]!;
        Assert.Equal("battleline", boyz["role"]!.GetValue<string>());
        Assert.Equal("75+", boyz["points"]!.GetValue<string>());
        Assert.False(boyz["isLegends"]!.GetValue<bool>());
        Assert.Equal("/data/factions/faction-00/datasheets/D00.json", boyz["path"]!.GetValue<string>());

        var warboss = summaries[1]!;
        Assert.Equal("other", warboss["role"]!.GetValue<string>());
        Assert.Null(warboss["points"]);
        Assert.True(warboss["isLegends"]!.GetValue<bool>());
    }

    [Fact]
    public void Datasheets_without_a_known_supplement_belong_to_the_codex()
    {
        var boyz = Datasheet("D00");

        Assert.Equal("codex", boyz["supplementKey"]!.GetValue<string>());
        Assert.Equal("None", boyz["supplementLabel"]!.GetValue<string>());
        Assert.False(boyz["isSupplement"]!.GetValue<bool>());
        Assert.Null(boyz["supplementSlug"]);
        Assert.Equal("Codex: Codex Orks", boyz["sourceName"]!.GetValue<string>());
    }

    [Fact]
    public void Abilities_resolve_references_keep_inline_ones_and_drop_unknown_ids()
    {
        var abilities = Datasheet("D00")["abilities"]!.AsArray();

        Assert.Equal(2, abilities.Count);
        var referenced = abilities[0]!;
        Assert.Equal("Leader", referenced["name"]!.GetValue<string>());
        Assert.Equal("This unit can lead.", referenced["description"]!.GetValue<string>());
        Assert.Equal("Core", referenced["type"]!.GetValue<string>());

        var inline = abilities[1]!;
        Assert.Equal("Waaagh!", inline["name"]!.GetValue<string>());
        Assert.Equal("", inline["id"]!.GetValue<string>());
        Assert.Equal("Datasheet", inline["type"]!.GetValue<string>());
    }

    [Fact]
    public void Wargear_profiles_of_one_weapon_are_grouped()
    {
        var wargear = Datasheet("D00")["wargear"]!.AsArray();

        Assert.Equal(["Big shoota", "Choppa", "Slugga"], wargear.Select(w => w!["name"]!.GetValue<string>()));
        var bigShoota = wargear[0]!;
        Assert.Equal("D00:big-shoota", bigShoota["id"]!.GetValue<string>());
        Assert.Equal("Ranged", bigShoota["type"]!.GetValue<string>());
        Assert.Equal(["focused", "sweeping"], bigShoota["profiles"]!.AsArray().Select(p => p!["profileName"]!.GetValue<string>()));
        Assert.Null(wargear[1]!["profiles"]![0]!["profileName"]);
    }

    [Fact]
    public void Model_costs_get_their_bracket_as_section_and_exact_duplicates_are_collapsed()
    {
        var costs = Datasheet("D00")["modelCosts"]!.AsArray();

        Assert.Equal(3, costs.Count);
        Assert.Equal("YOUR 1ST UNIT COSTS", costs[0]!["section"]!.GetValue<string>());
        Assert.Equal("80", costs[0]!["cost"]!.GetValue<string>());
        Assert.Equal("YOUR 2ND + UNIT COSTS", costs[2]!["section"]!.GetValue<string>());
    }

    [Fact]
    public void Leaders_are_linked_by_slug_and_unknown_ones_are_marked()
    {
        var leaders = Datasheet("D00")["leaders"]!.AsArray();

        Assert.Equal(["plain-01", "Unknown"], leaders.Select(l => l!["slug"]!.GetValue<string>()));
    }

    [Fact]
    public void Stratagem_ids_and_keywords_are_taken_over()
    {
        var boyz = Datasheet("D00");

        Assert.Equal(["ST1"], boyz["stratagemIds"]!.AsArray().Select(i => i!.GetValue<string>()));
        Assert.Equal(3, boyz["keywords"]!.AsArray().Count);
        Assert.Equal("Infantry", boyz["keywords"]![0]!["keyword"]!.GetValue<string>());
    }

    [Fact]
    public void Detachments_carry_their_rules_sorted_by_name_and_repeated_names_get_distinct_slugs()
    {
        var detachments = Faction.Manifest["detachments"]!.AsArray();

        Assert.Equal(["Blitz Brigade", "Blitz Brigade", "Bully Boyz"], detachments.Select(d => d!["name"]!.GetValue<string>()));
        Assert.Equal("blitz-brigade", detachments[0]!["slug"]!.GetValue<string>());
        Assert.Equal("blitz-brigade-dt3", detachments[1]!["slug"]!.GetValue<string>());

        var blitz = detachments[0]!;
        Assert.Equal("2", blitz["dp"]!.GetValue<string>());
        Assert.Equal("Take and Hold", blitz["forceDisposition"]!.GetValue<string>());
        Assert.Equal("Some Chapter", blitz["chapterDp"]![0]!["keyword"]!.GetValue<string>());
        Assert.Equal(["Aaa Kit", "Zzz Gubbinz"], blitz["enhancements"]!.AsArray().Select(e => e!["name"]!.GetValue<string>()));
        Assert.True(blitz["enhancements"]![0]!["upgrade"]!.GetValue<bool>());
        Assert.Null(blitz["enhancements"]![1]!["upgrade"]);
        Assert.Equal("Support text", blitz["enhancements"]![0]!["supportLeader"]!.GetValue<string>());
        Assert.Single(blitz["abilities"]!.AsArray());
        Assert.Single(blitz["stratagems"]!.AsArray());
        Assert.Equal("Boarding Actions", detachments[1]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Only_stratagems_without_a_faction_are_core_stratagems_sorted_by_name()
    {
        Assert.Equal(["ALPHA ACTION", "COMMAND RE-ROLL"], Data.CoreStratagems.Select(s => s!["name"]!.GetValue<string>()));
        Assert.Equal(["id", "factionId", "name", "type", "cpCost", "legend", "turn", "phase", "description", "detachment"], Data.CoreStratagems[0]!.AsObject().Select(p => p.Key));
    }
}
