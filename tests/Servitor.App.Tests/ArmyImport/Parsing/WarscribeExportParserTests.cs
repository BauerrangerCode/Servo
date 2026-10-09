using Servitor.App.ArmyImport.Parsing;

namespace Servitor.App.Tests.ArmyImport.Parsing;

public class WarscribeExportParserTests
{
    private static string ReadArmy(string fileName) => File.ReadAllText(TestPaths.DataFile(fileName));

    [Fact]
    public void Parses_the_three_detachment_Ork_sample_end_to_end()
    {
        var result = WarscribeExportParser.Parse(ReadArmy("Army1.txt"));

        Assert.True(result.IsSuccess);
        var export = result.Export!;
        Assert.Equal("Xenos - Orks", export.FactionKeyword);
        Assert.Equal(["Green Tide", "Taktikal Brigade", "Madcap Meks"], export.DetachmentNames);
        Assert.Equal(2005, export.TotalArmyPoints);
        Assert.Equal(3, export.WarlordCharacterNumber);
        Assert.Equal("Ghazghkull Thraka", export.WarlordName);
        Assert.Equal(16, export.DeclaredUnitCount);
        Assert.Equal(7, export.Characters.Count);
        Assert.Equal(9, export.Units.Count);
        Assert.Empty(export.Warnings);
    }

    [Fact]
    public void Resolves_the_warlord_flag_and_matches_it_to_the_header()
    {
        var export = WarscribeExportParser.Parse(ReadArmy("Army1.txt")).Export!;

        var warlord = Assert.Single(export.Characters, c => c.IsWarlord);
        Assert.Equal("Ghazghkull Thraka", warlord.Name);
        Assert.Equal(3, warlord.CharacterNumber);
    }

    [Fact]
    public void Resolves_leader_attachment_from_the_units_Led_by_suffix()
    {
        var export = WarscribeExportParser.Parse(ReadArmy("Army1.txt")).Export!;

        var boyzLedByPainboy = Assert.Single(export.Units, u => u.Name == "Boyz" && u.LedByCharacterName == "Painboy");
        var boyzLedByWeirdboy = Assert.Single(export.Units, u => u.Name == "Boyz" && u.LedByCharacterName == "Weirdboy");
        Assert.NotEqual(boyzLedByPainboy, boyzLedByWeirdboy);

        var painboy = Assert.Single(export.Characters, c => c.Name == "Painboy");
        Assert.Equal("Boyz", painboy.LeadingUnitName);
    }

    [Fact]
    public void Parses_enhancements_and_strips_the_Upgrade_qualifier_from_the_name()
    {
        var export = WarscribeExportParser.Parse(ReadArmy("Army1.txt")).Export!;

        var kommandosWithEnhancement = Assert.Single(export.Units, u => u.Enhancement is not null && u.Name == "Kommandos");
        Assert.Equal("Throat-slittas", kommandosWithEnhancement.Enhancement!.Name);
        Assert.Equal(15, kommandosWithEnhancement.Enhancement.Points);
        Assert.True(kommandosWithEnhancement.Enhancement.HasUpgradeQualifier);

        var bigMek = Assert.Single(export.Characters, c => c.Name == "Big Mek Dakkarig" && c.Enhancement is not null);
        Assert.Equal("Mekwaaagh! Mastermind", bigMek.Enhancement!.Name);
        Assert.Equal(30, bigMek.Enhancement.Points);
        Assert.False(bigMek.Enhancement.HasUpgradeQualifier);
    }

    [Fact]
    public void Preserves_curly_apostrophes_in_wargear_names()
    {
        var export = WarscribeExportParser.Parse(ReadArmy("Army1.txt")).Export!;

        var ghazghkull = Assert.Single(export.Characters, c => c.Name == "Ghazghkull Thraka");
        var morksRoar = Assert.Single(ghazghkull.Wargear, w => w.Name.StartsWith("Mork", StringComparison.Ordinal));
        Assert.Contains('’', morksRoar.Name);
    }

    [Fact]
    public void Parses_the_second_three_detachment_Ork_sample_end_to_end()
    {
        var result = WarscribeExportParser.Parse(ReadArmy("Army2.txt"));

        Assert.True(result.IsSuccess);
        var export = result.Export!;
        Assert.Equal(2000, export.TotalArmyPoints);
        Assert.Equal(14, export.DeclaredUnitCount);
        Assert.Equal(5, export.Characters.Count);
        Assert.Equal(9, export.Units.Count);
    }

    [Fact]
    public void Parses_a_sample_with_no_enhancements_or_leader_attachments()
    {
        var result = WarscribeExportParser.Parse(ReadArmy("Army3.txt"));

        Assert.True(result.IsSuccess);
        var export = result.Export!;
        Assert.Equal("Imperium - Space Wolves", export.FactionKeyword);
        Assert.Equal(1660, export.TotalArmyPoints);
        Assert.Equal(12, export.DeclaredUnitCount);
        Assert.All(export.Characters, c => Assert.Null(c.Enhancement));
        Assert.All(export.Characters, c => Assert.Null(c.LeadingUnitName));
        Assert.All(export.Units, u => Assert.Null(u.LedByCharacterName));
    }

    [Theory]
    [InlineData("Army5.txt")]
    [InlineData("Army6.txt")]
    [InlineData("Army7.txt")]
    public void Rejects_the_compact_dialect_cleanly_instead_of_misparsing_it(string fileName)
    {
        // Army5-7 use a different, unvalidated export dialect (no footer, "with"-phrased wargear, no Leading:/
        // [Led by:]) — the project owner decided (2026-09-28) this parser targets only the grammar validated
        // against the live reference app; anything else is a clean rejection, not a parser target.
        var result = WarscribeExportParser.Parse(ReadArmy(fileName));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("doesn't look like a Warscribe 40K"));
    }

    [Fact]
    public void Empty_input_is_a_handled_failure_not_an_exception()
    {
        var result = WarscribeExportParser.Parse("");

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Garbage_text_is_a_handled_failure_not_an_exception()
    {
        var result = WarscribeExportParser.Parse("this is not an army list at all, just some random pasted text");

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void A_points_total_mismatch_is_a_handled_failure()
    {
        var tampered = ReadArmy("Army1.txt").Replace("TOTAL ARMY POINTS: 2005pts", "TOTAL ARMY POINTS: 9999pts");

        var result = WarscribeExportParser.Parse(tampered);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("TOTAL ARMY POINTS"));
    }

    [Fact]
    public void A_unit_count_mismatch_is_a_handled_failure()
    {
        var tampered = ReadArmy("Army1.txt").Replace("NUMBER OF UNITS: 16", "NUMBER OF UNITS: 3");

        var result = WarscribeExportParser.Parse(tampered);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("NUMBER OF UNITS"));
    }

    [Theory]
    [InlineData("20x Boyz (180 pts)", "99999999999x Boyz (180 pts)")]
    [InlineData("20x Boyz (180 pts)", "20x Boyz (99999999999 pts)")]
    [InlineData("1x Big Mek Dakkarig (135 pts): 1x Multi-busta Launcha", "99999999999x Big Mek Dakkarig (135 pts): 1x Multi-busta Launcha")]
    [InlineData("Char2: 1x Big Mek", "Char99999999999: 1x Big Mek")]
    [InlineData("Mekwaaagh! Mastermind (+30pts)", "Mekwaaagh! Mastermind (+99999999999pts)")]
    [InlineData("1x Multi-busta Launcha, 1x Stompy Feet", "99999999999x Multi-busta Launcha, 1x Stompy Feet")]
    [InlineData("TOTAL ARMY POINTS: 2005pts", "TOTAL ARMY POINTS: 99999999999pts")]
    [InlineData("WARLORD: Char3:", "WARLORD: Char99999999999:")]
    public void A_number_beyond_the_int_range_is_a_handled_failure_not_an_exception(string original, string tampered)
    {
        var text = ReadArmy("Army1.txt");
        Assert.Contains(original, text);

        var result = WarscribeExportParser.Parse(text.Replace(original, tampered));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("too large", StringComparison.Ordinal));
    }

    [Fact]
    public void A_missing_warlord_marker_is_a_handled_failure()
    {
        var tampered = ReadArmy("Army1.txt").Replace(", Warlord", "");

        var result = WarscribeExportParser.Parse(tampered);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("Warlord"));
    }
}
