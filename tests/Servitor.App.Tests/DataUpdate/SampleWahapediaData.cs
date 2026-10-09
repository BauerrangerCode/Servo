using System.Text;
using Servitor.DataUpdate.Wahapedia;

namespace Servitor.App.Tests.DataUpdate;

/// <summary>Small, hand-made Wahapedia CSV exports with a few deliberate quirks, in the source's pipe-separated format.</summary>
internal static class SampleWahapediaData
{
    private static string Datasheet(string id, string name, string factionId, string sourceId, string legend, string role, string loadout, string isVirtual, string link) =>
        string.Join('|', id, name, factionId, sourceId, legend, role, "false", loadout, "", isVirtual, "", "", "", "", link);

    public static string Csv(string header, params string[] rows) =>
        header + "|\r\n" + string.Concat(rows.Select(r => r + "|\r\n"));

    /// <param name="factionCount">
    /// Factions F00..Fnn. F00 owns the interesting data; every other faction has one plain datasheet, so the update's
    /// "at least ten factions" sanity check passes.
    /// </param>
    public static Dictionary<string, string> Tables(int factionCount = 12)
    {
        var factions = Enumerable.Range(0, factionCount).Select(i => $"F{i:00}|Faction {i:00}|https://example/f{i:00}").ToArray();
        var plainSheets = Enumerable.Range(1, factionCount - 1)
            .Select(i => Datasheet($"D{i:00}", $"Plain {i:00}", $"F{i:00}", "S1", "", "character", "", "false", $"https://example/d{i:00}"))
            .ToArray();

        return new Dictionary<string, string>
        {
            ["Factions.csv"] = Csv("id|name|link", factions),
            ["Source.csv"] = Csv("id|name|type|edition|version|errata_date|errata_link", "S1|Codex Orks|Codex|11|1||", "S2|Ork Legends (Warhammer Legends)|Legends|11|1||"),
            ["Datasheets.csv"] = Csv(
                "id|name|faction_id|source_id|legend|role|is_support|loadout|transport|virtual|leader_head|leader_footer|damaged_w|damaged_description|link",
                [
                    Datasheet("D00", "Boyz", "F00", "S1", "<p class=\"legend\">Fluff</p>", "battleline", "Slugga", "false", "https://example/boyz"),
                    Datasheet("D99", "Virtual thing", "F00", "S1", "", "other", "", "true", ""),
                    Datasheet("D98", "Old Warboss", "F00", "S2", "", "character", "", "false", ""),
                    .. plainSheets,
                ]),
            ["Datasheets_abilities.csv"] = Csv(
                "datasheet_id|line|ability_id|model|name|description|type|parameter",
                "D00|1|A1||||Core|",
                "D00|2||| Waaagh! |Inline <b>rules</b>|Datasheet|",
                "D00|3|A404||||Faction|"),
            ["Datasheets_keywords.csv"] = Csv("datasheet_id|keyword|model|is_faction_keyword", "D00|Infantry||false", "D00|Battleline||false", "D00|Orks||true"),
            ["Datasheets_models.csv"] = Csv("datasheet_id|line|name|m|t|sv|inv_sv|inv_sv_descr|w|ld|oc|base_size|base_size_descr", "D00|1|Boy|6\"|5|5+|||1|7+|2|32mm|"),
            ["Datasheets_options.csv"] = Csv("datasheet_id|line|button|description", "D00|1||Up to 1 boy may take a big shoota"),
            ["Datasheets_wargear.csv"] = Csv(
                "datasheet_id|line|line_in_wargear|dice|name|description|range|type|A|BS_WS|S|AP|D",
                "D00|1|1||Big shoota - focused|Rapid Fire 1|36\"|Ranged|3|5+|5|0|1",
                "D00|2|2||Big shoota - sweeping|Blast|36\"|Ranged|6|5+|5|0|1",
                "D00|3|1||Choppa|Extra Attacks|Melee|Melee|3|3+|4|-1|1",
                "D00|4|1||Slugga||12\"|Ranged|1|5+|4|0|1"),
            ["Datasheets_unit_composition.csv"] = Csv("datasheet_id|line|description", "D00|1|10 Boyz"),
            ["Datasheets_models_cost.csv"] = Csv(
                "datasheet_id|line|description|cost",
                "D00|1|YOUR 1ST UNIT COSTS|",
                "D00|2|10 models|80",
                "D00|3|20 models|160",
                "D00|4|20 models|160",
                "D00|5|YOUR 2ND + UNIT COSTS|",
                "D00|6|10 models|75"),
            ["Datasheets_stratagems.csv"] = Csv("datasheet_id|stratagem_id", "D00|ST1"),
            ["Datasheets_leader.csv"] = Csv("leader_id|attached_id", "D00|D01", "D00|D77"),
            ["Stratagems.csv"] = Csv(
                "faction_id|name|id|type|cp_cost|legend|turn|phase|detachment|detachment_id|description",
                "F00|GET STUCK IN|ST1|Blitz Brigade Stratagem – Battle Tactic|1|Fluff|Your turn|Fight phase|Blitz Brigade|DT1|<b>WHEN:</b> Fight",
                "|COMMAND RE-ROLL|ST9|Core – Battle Tactic Stratagem|1|Fluff|Either|Any phase|||<b>WHEN:</b> Any",
                "|ALPHA ACTION|ST0|Core – Strategic Ploy Stratagem|1|Fluff|Either|Any phase|||<b>WHEN:</b> Any"),
            ["Abilities.csv"] = Csv("id|name|legend|faction_id|description", "A1|Leader|Fluff||<div class=\"abName\">LEADER</div>This unit can lead."),
            ["Enhancements.csv"] = Csv(
                "faction_id|name|id|cost|detachment|detachment_id|upgrade|legend|description|support_leader",
                "F00|Zzz Gubbinz|E2|25|Blitz Brigade|DT1|false|Fluff|<p>Text</p>|",
                "F00|Aaa Kit|E1|10|Blitz Brigade|DT1|true|Fluff|Upgrade text|Support text"),
            ["Detachment_abilities.csv"] = Csv("id|faction_id|name|legend|description|detachment|detachment_id", "DA1|F00|Speed|Fluff|Faster|Blitz Brigade|DT1"),
            ["Detachments.csv"] = Csv(
                "id|faction_id|name|legend|type|dp|force_disposition",
                "DT2|F00|Bully Boyz|Fluff||1|Purge the Foe",
                "DT1|F00|Blitz Brigade|Fluff||2|Take and Hold",
                "DT3|F00|Blitz Brigade|Fluff|Boarding Actions|||"),
            ["Detachments_chapter_dp.csv"] = Csv("detachment_id|keyword|dp", "DT1|Some Chapter|3"),
            ["Last_update.csv"] = Csv("last_update", "2026-09-23 10:12:23"),
        };
    }

    public static WahapediaTables Parsed(int factionCount = 12) => WahapediaTables.Parse(Tables(factionCount));

    public static string CorePage(int chapterCount = 12)
    {
        var page = new StringBuilder("<html><h1>Core Rules</h1><div>Contents ...</div>");
        for (var i = 1; i <= chapterCount; i++)
        {
            page.Append($"<h3 id=\"c{i}\" class=\"outline_header\" style=\"display:flex; align-items:baseline;\">Chapter {i} {i:00}</h3>")
                .Append($"<p class=\"legend\">Intro {i}</p>")
                .Append($"<h2 id=\"s{i}\" style=\"display:flex;\">Section <span class=\"h_number\">{i:00}.01</span></h2>Text {i}<br>");
        }

        return page.Append("</html>").ToString();
    }
}
