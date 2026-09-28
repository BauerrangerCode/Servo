namespace Servitor.App.ArmyImport.Parsing;

/// <summary>One item in a Char/unit line's wargear list, e.g. "18x Slugga" -&gt; (18, "Slugga").</summary>
public sealed record ParsedWargearItem(int Quantity, string Name);

/// <summary>
/// A parsed "Enhancement: &lt;Name&gt; (+&lt;N&gt;pts)" line. <paramref name="HasUpgradeQualifier"/> records whether
/// the name carried a trailing "(Upgrade)" qualifier (e.g. "'Ardboyz (Upgrade)"), stripped from
/// <paramref name="Name"/> — the resolver cross-checks this against the catalog's own Enhancement.IsUpgrade flag.
/// </summary>
public sealed record ParsedEnhancement(string Name, int Points, bool HasUpgradeQualifier);

/// <summary>A parsed "Char&lt;N&gt;: ..." line plus its optional Enhancement:/Leading: follow-lines.</summary>
public sealed record ParsedCharacter(
    int CharacterNumber,
    string Name,
    int Points,
    IReadOnlyList<ParsedWargearItem> Wargear,
    bool IsWarlord,
    ParsedEnhancement? Enhancement,
    string? LeadingUnitName);

/// <summary>A parsed non-character unit line plus its optional Enhancement: follow-line and "[Led by: ...]" suffix.</summary>
public sealed record ParsedUnit(
    int ModelCount,
    string Name,
    int Points,
    IReadOnlyList<ParsedWargearItem> Wargear,
    string? LedByCharacterName,
    ParsedEnhancement? Enhancement);

/// <summary>
/// The full text export, parsed into structured fields but not yet resolved against the rules-data catalog (see
/// <c>Servitor.App.ArmyImport.Resolution.ArmyExportResolver</c> for that step). <paramref name="Warnings"/> holds
/// non-fatal consistency issues (e.g. a "Leading:" reference with no matching "[Led by:]" counterpart) found
/// during parsing; they do not fail the parse.
/// </summary>
public sealed record ParsedArmyExport(
    string FactionKeyword,
    IReadOnlyList<string> DetachmentNames,
    int TotalArmyPoints,
    int WarlordCharacterNumber,
    string WarlordName,
    int DeclaredUnitCount,
    IReadOnlyList<ParsedCharacter> Characters,
    IReadOnlyList<ParsedUnit> Units,
    IReadOnlyList<string> Warnings);
