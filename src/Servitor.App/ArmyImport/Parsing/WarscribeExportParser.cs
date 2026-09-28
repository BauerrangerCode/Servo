using System.Text.RegularExpressions;

namespace Servitor.App.ArmyImport.Parsing;

/// <summary>
/// Parses the plain-text army list export produced by Warscribe 40K (grammar documented in
/// docs/Reference-App-Analysis.md Section 3.1, validated end-to-end against the live reference app). Pure text in,
/// structured result out — no dependency on the rules-data catalog, so it is fully unit-testable on its own. Catalog
/// resolution into real <c>Datasheet</c>/<c>Detachment</c>/<c>Enhancement</c> objects happens one layer up, in
/// <c>Servitor.App.ArmyImport.Resolution.ArmyExportResolver</c>.
/// </summary>
public static partial class WarscribeExportParser
{
    // The reference app's own footer signature — the one place this specific export format is unambiguously
    // identifiable, so a paste in a different (or no) format is rejected here rather than misparsed.
    private const string FooterSignature = "Created with Warscribe: 40K";

    public static ImportParseResult Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ImportParseResult.Failure("Nothing was pasted.");
        }

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (!lines.Any(l => l.Trim() == FooterSignature))
        {
            return ImportParseResult.Failure(
                $"This doesn't look like a Warscribe 40K plain-text export (the \"{FooterSignature}\" footer line is missing).");
        }

        var errors = new List<string>();
        var i = 0;
        var header = ParseHeader(lines, ref i, errors);
        if (header is null ||
            !TryReadHeaderFields(header, errors, out var factionKeyword, out var detachmentNames,
                out var totalPoints, out var warlordNumber, out var warlordName, out var unitCount))
        {
            return ImportParseResult.Failure(errors);
        }

        i = SkipBlank(lines, i);
        var (characters, afterCharacters) = ParseCharacterBlock(lines, i, errors);
        i = SkipBlank(lines, afterCharacters);
        var (units, _) = ParseUnitBlock(lines, i, errors);

        if (errors.Count > 0)
        {
            return ImportParseResult.Failure(errors);
        }

        var warnings = new List<string>();
        ValidateWarlord(characters, warlordNumber, warlordName, errors);
        ValidateLeaderAttachments(characters, units, warnings);
        ValidatePointsTotal(characters, units, totalPoints, errors);
        ValidateUnitCount(characters, units, unitCount, errors);

        return errors.Count > 0
            ? ImportParseResult.Failure(errors)
            : ImportParseResult.Success(new ParsedArmyExport(
                factionKeyword, detachmentNames, totalPoints, warlordNumber, warlordName, unitCount,
                characters, units, warnings));
    }

    private static Dictionary<string, string>? ParseHeader(string[] lines, ref int i, List<string> errors)
    {
        i = SkipBlank(lines, i);
        if (i >= lines.Length || !IsDelimiterLine(lines[i]))
        {
            errors.Add("Expected the header block's opening '+' delimiter line.");
            return null;
        }

        i++;
        var fields = new Dictionary<string, string>();
        while (i < lines.Length && !IsDelimiterLine(lines[i]))
        {
            var line = lines[i].Trim();
            if (line.Length > 0)
            {
                var match = HeaderFieldRegex().Match(line);
                if (match.Success)
                {
                    fields[match.Groups[1].Value.Trim()] = match.Groups[2].Value.Trim();
                }
                else
                {
                    errors.Add($"Unrecognized header line: \"{line}\"");
                }
            }

            i++;
        }

        if (i >= lines.Length)
        {
            errors.Add("Header block is missing its closing '+' delimiter line.");
            return null;
        }

        i++;
        return fields;
    }

    private static bool TryReadHeaderFields(
        Dictionary<string, string> header,
        List<string> errors,
        out string factionKeyword,
        out List<string> detachmentNames,
        out int totalPoints,
        out int warlordNumber,
        out string warlordName,
        out int unitCount)
    {
        factionKeyword = "";
        detachmentNames = [];
        totalPoints = 0;
        warlordNumber = 0;
        warlordName = "";
        unitCount = 0;
        var ok = true;

        if (!header.TryGetValue("FACTION KEYWORD", out var faction))
        {
            errors.Add("Header is missing the FACTION KEYWORD field.");
            ok = false;
        }
        else
        {
            factionKeyword = faction;
        }

        if (!header.TryGetValue("DETACHMENT", out var detachmentValue))
        {
            errors.Add("Header is missing the DETACHMENT field.");
            ok = false;
        }
        else
        {
            detachmentNames = [.. detachmentValue.Split(" + ").Select(d => d.Trim())];
        }

        if (!header.TryGetValue("TOTAL ARMY POINTS", out var pointsValue) ||
            TotalPointsRegex().Match(pointsValue.Trim()) is not { Success: true } pointsMatch)
        {
            errors.Add("Header's TOTAL ARMY POINTS field is missing or not of the form \"<N>pts\".");
            ok = false;
        }
        else
        {
            totalPoints = int.Parse(pointsMatch.Groups[1].Value);
        }

        if (!header.TryGetValue("WARLORD", out var warlordValue))
        {
            errors.Add("Header is missing the WARLORD field.");
            ok = false;
        }
        else
        {
            var match = WarlordFieldRegex().Match(warlordValue.Trim());
            if (!match.Success)
            {
                errors.Add($"Header's WARLORD field \"{warlordValue}\" doesn't match the expected \"Char<N>: <Name>\" shape.");
                ok = false;
            }
            else
            {
                warlordNumber = int.Parse(match.Groups[1].Value);
                warlordName = match.Groups[2].Value.Trim();
            }
        }

        if (!header.TryGetValue("NUMBER OF UNITS", out var unitCountValue) || !int.TryParse(unitCountValue.Trim(), out unitCount))
        {
            errors.Add("Header's NUMBER OF UNITS field is missing or not a number.");
            ok = false;
        }

        return ok;
    }

    private static (List<ParsedCharacter> Characters, int NextIndex) ParseCharacterBlock(string[] lines, int start, List<string> errors)
    {
        var characters = new List<ParsedCharacter>();
        ParsedCharacter? current = null;
        ParsedEnhancement? pendingEnhancement = null;
        string? pendingLeading = null;
        var i = start;

        void Flush()
        {
            if (current is { } c)
            {
                characters.Add(c with { Enhancement = pendingEnhancement, LeadingUnitName = pendingLeading });
            }

            pendingEnhancement = null;
            pendingLeading = null;
        }

        while (i < lines.Length && !IsBlank(lines[i]))
        {
            var line = lines[i].Trim();
            if (TryParseCharacterLine(line, errors, out var parsed))
            {
                Flush();
                current = parsed;
            }
            else if (line.StartsWith("Enhancement: ", StringComparison.Ordinal) && current is not null)
            {
                pendingEnhancement = ParseEnhancementLine(line, errors);
            }
            else if (line.StartsWith("Leading: ", StringComparison.Ordinal) && current is not null)
            {
                pendingLeading = line["Leading: ".Length..].Trim();
            }
            else
            {
                errors.Add($"Unrecognized line in the character block: \"{line}\"");
            }

            i++;
        }

        Flush();
        return (characters, i);
    }

    private static (List<ParsedUnit> Units, int NextIndex) ParseUnitBlock(string[] lines, int start, List<string> errors)
    {
        var units = new List<ParsedUnit>();
        ParsedUnit? current = null;
        ParsedEnhancement? pendingEnhancement = null;
        var i = start;

        void Flush()
        {
            if (current is { } u)
            {
                units.Add(u with { Enhancement = pendingEnhancement });
            }

            pendingEnhancement = null;
        }

        while (i < lines.Length && !IsBlank(lines[i]))
        {
            var line = lines[i].Trim();
            if (TryParseUnitLine(line, errors, out var parsed))
            {
                Flush();
                current = parsed;
            }
            else if (line.StartsWith("Enhancement: ", StringComparison.Ordinal) && current is not null)
            {
                pendingEnhancement = ParseEnhancementLine(line, errors);
            }
            else
            {
                errors.Add($"Unrecognized line in the unit block: \"{line}\"");
            }

            i++;
        }

        Flush();
        return (units, i);
    }

    private static bool TryParseCharacterLine(string line, List<string> errors, out ParsedCharacter? character)
    {
        var match = CharacterLineRegex().Match(line);
        if (!match.Success)
        {
            character = null;
            return false;
        }

        var number = int.Parse(match.Groups[1].Value);
        var qty = int.Parse(match.Groups[2].Value);
        var name = match.Groups[3].Value.Trim();
        var points = int.Parse(match.Groups[4].Value);
        var tail = match.Groups[5].Value;

        var isWarlord = false;
        if (tail.EndsWith(", Warlord", StringComparison.Ordinal))
        {
            isWarlord = true;
            tail = tail[..^", Warlord".Length];
        }

        if (qty != 1)
        {
            errors.Add($"Char{number}: {name} has quantity {qty}x — expected exactly 1x for a character.");
        }

        var wargear = ParseWargearItems(tail, errors, $"Char{number}: {name}");
        character = new ParsedCharacter(number, name, points, wargear, isWarlord, null, null);
        return true;
    }

    private static bool TryParseUnitLine(string line, List<string> errors, out ParsedUnit? unit)
    {
        var match = UnitLineRegex().Match(line);
        if (!match.Success)
        {
            unit = null;
            return false;
        }

        var qty = int.Parse(match.Groups[1].Value);
        var name = match.Groups[2].Value.Trim();
        var points = int.Parse(match.Groups[3].Value);
        var tail = match.Groups[4].Value;

        string? ledBy = null;
        var ledByMatch = LedBySuffixRegex().Match(tail);
        if (ledByMatch.Success)
        {
            ledBy = ledByMatch.Groups[1].Value.Trim();
            tail = tail[..ledByMatch.Index];
        }

        var wargear = ParseWargearItems(tail, errors, $"{qty}x {name}");
        unit = new ParsedUnit(qty, name, points, wargear, ledBy, null);
        return true;
    }

    private static ParsedEnhancement? ParseEnhancementLine(string line, List<string> errors)
    {
        var match = EnhancementLineRegex().Match(line);
        if (!match.Success)
        {
            errors.Add($"Unrecognized Enhancement line: \"{line}\"");
            return null;
        }

        var name = match.Groups[1].Value.Trim();
        var points = int.Parse(match.Groups[2].Value);

        const string upgradeSuffix = " (Upgrade)";
        var hasUpgrade = name.EndsWith(upgradeSuffix, StringComparison.Ordinal);
        if (hasUpgrade)
        {
            name = name[..^upgradeSuffix.Length];
        }

        return new ParsedEnhancement(name, points, hasUpgrade);
    }

    private static List<ParsedWargearItem> ParseWargearItems(string tail, List<string> errors, string context)
    {
        var items = new List<ParsedWargearItem>();
        foreach (var part in tail.Split(", "))
        {
            var trimmed = part.Trim();
            var match = WargearItemRegex().Match(trimmed);
            if (match.Success)
            {
                items.Add(new ParsedWargearItem(int.Parse(match.Groups[1].Value), match.Groups[2].Value.Trim()));
            }
            else
            {
                errors.Add($"Unrecognized wargear item \"{trimmed}\" in {context}.");
            }
        }

        return items;
    }

    private static void ValidateWarlord(IReadOnlyList<ParsedCharacter> characters, int warlordNumber, string warlordName, List<string> errors)
    {
        var warlords = characters.Where(c => c.IsWarlord).ToList();
        if (warlords.Count != 1)
        {
            errors.Add($"Expected exactly one character marked \", Warlord\", found {warlords.Count}.");
            return;
        }

        var warlord = warlords[0];
        if (warlord.CharacterNumber != warlordNumber || warlord.Name != warlordName)
        {
            errors.Add(
                $"Header's WARLORD field (\"Char{warlordNumber}: {warlordName}\") doesn't match the character marked " +
                $"\", Warlord\" in the body (\"Char{warlord.CharacterNumber}: {warlord.Name}\").");
        }
    }

    // The "Leading: X" (character side) and "[Led by: Y]" (unit side) lines describe the same attachment from both
    // directions (docs/Reference-App-Analysis.md 3.1). The resolver relies on the unit-side direction alone, since
    // it unambiguously names one physical unit line even when several units share a name; this only cross-checks
    // the redundant character-side direction and reports a mismatch as a warning, not a hard failure.
    private static void ValidateLeaderAttachments(IReadOnlyList<ParsedCharacter> characters, IReadOnlyList<ParsedUnit> units, List<string> warnings)
    {
        foreach (var character in characters.Where(c => c.LeadingUnitName is not null))
        {
            var matched = units.Any(u => u.Name == character.LeadingUnitName && u.LedByCharacterName == character.Name);
            if (!matched)
            {
                warnings.Add(
                    $"Char{character.CharacterNumber}: {character.Name} says \"Leading: {character.LeadingUnitName}\", " +
                    "but no unit with a matching \"[Led by: ...]\" reference was found.");
            }
        }
    }

    private static void ValidatePointsTotal(IReadOnlyList<ParsedCharacter> characters, IReadOnlyList<ParsedUnit> units, int totalPoints, List<string> errors)
    {
        var sum = characters.Sum(c => c.Points + (c.Enhancement?.Points ?? 0))
            + units.Sum(u => u.Points + (u.Enhancement?.Points ?? 0));
        if (sum != totalPoints)
        {
            errors.Add($"Parsed points ({sum}) don't match the header's TOTAL ARMY POINTS ({totalPoints}).");
        }
    }

    private static void ValidateUnitCount(IReadOnlyList<ParsedCharacter> characters, IReadOnlyList<ParsedUnit> units, int unitCount, List<string> errors)
    {
        var actual = characters.Count + units.Count;
        if (actual != unitCount)
        {
            errors.Add($"Parsed {actual} units/characters, but the header's NUMBER OF UNITS says {unitCount}.");
        }
    }

    private static bool IsBlank(string line) => string.IsNullOrWhiteSpace(line);

    private static bool IsDelimiterLine(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length > 0 && trimmed.All(c => c == '+');
    }

    private static int SkipBlank(string[] lines, int i)
    {
        while (i < lines.Length && IsBlank(lines[i]))
        {
            i++;
        }

        return i;
    }

    [GeneratedRegex(@"^\+\s*([A-Z ]+):\s*(.*)$")]
    private static partial Regex HeaderFieldRegex();

    [GeneratedRegex(@"^(\d+)\s*pts$")]
    private static partial Regex TotalPointsRegex();

    [GeneratedRegex(@"^Char(\d+):\s*(.+)$")]
    private static partial Regex WarlordFieldRegex();

    [GeneratedRegex(@"^Char(\d+): (\d+)x (.+?) \((\d+) pts\): (.+)$")]
    private static partial Regex CharacterLineRegex();

    [GeneratedRegex(@"^(\d+)x (.+?) \((\d+) pts\): (.+)$")]
    private static partial Regex UnitLineRegex();

    [GeneratedRegex(@" \[Led by: (.+)\]$")]
    private static partial Regex LedBySuffixRegex();

    [GeneratedRegex(@"^Enhancement: (.+) \(\+(\d+)\s*pts\)$")]
    private static partial Regex EnhancementLineRegex();

    [GeneratedRegex(@"^(\d+)x (.+)$")]
    private static partial Regex WargearItemRegex();
}
