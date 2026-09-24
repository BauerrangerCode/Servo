namespace Servitor.DataUpdate.Wahapedia;

/// <summary>The Wahapedia CSV exports the update reads, by table name, and where each one is downloaded from.</summary>
public sealed record WahapediaTables(
    IReadOnlyList<CsvRow> Factions,
    IReadOnlyList<CsvRow> Sources,
    IReadOnlyList<CsvRow> Datasheets,
    IReadOnlyList<CsvRow> DatasheetAbilities,
    IReadOnlyList<CsvRow> DatasheetKeywords,
    IReadOnlyList<CsvRow> DatasheetModels,
    IReadOnlyList<CsvRow> DatasheetOptions,
    IReadOnlyList<CsvRow> DatasheetWargear,
    IReadOnlyList<CsvRow> DatasheetUnitComposition,
    IReadOnlyList<CsvRow> DatasheetModelCosts,
    IReadOnlyList<CsvRow> DatasheetStratagems,
    IReadOnlyList<CsvRow> DatasheetLeaders,
    IReadOnlyList<CsvRow> Stratagems,
    IReadOnlyList<CsvRow> Abilities,
    IReadOnlyList<CsvRow> Enhancements,
    IReadOnlyList<CsvRow> DetachmentAbilities,
    IReadOnlyList<CsvRow> Detachments,
    IReadOnlyList<CsvRow> DetachmentChapterDp,
    string? LastUpdate)
{
    public const string BaseUrl = "https://wahapedia.ru/wh40k11ed/";

    public const string LastUpdateFile = "Last_update.csv";

    /// <summary>Every CSV file needed, in the order of the constructor parameters above (plus the ones only depot's cache keeps).</summary>
    public static readonly IReadOnlyList<string> FileNames =
    [
        "Factions.csv",
        "Source.csv",
        "Datasheets.csv",
        "Datasheets_abilities.csv",
        "Datasheets_keywords.csv",
        "Datasheets_models.csv",
        "Datasheets_options.csv",
        "Datasheets_wargear.csv",
        "Datasheets_unit_composition.csv",
        "Datasheets_models_cost.csv",
        "Datasheets_stratagems.csv",
        "Datasheets_leader.csv",
        "Stratagems.csv",
        "Abilities.csv",
        "Enhancements.csv",
        "Detachment_abilities.csv",
        "Detachments.csv",
        "Detachments_chapter_dp.csv",
        LastUpdateFile,
    ];

    /// <param name="csvByFileName">The raw CSV text of every file in <see cref="FileNames"/>.</param>
    public static WahapediaTables Parse(IReadOnlyDictionary<string, string> csvByFileName)
    {
        List<CsvRow> Table(string fileName) => WahapediaCsv.Parse(csvByFileName[fileName]);

        return new WahapediaTables(
            Table("Factions.csv"),
            Table("Source.csv"),
            Table("Datasheets.csv"),
            Table("Datasheets_abilities.csv"),
            Table("Datasheets_keywords.csv"),
            Table("Datasheets_models.csv"),
            Table("Datasheets_options.csv"),
            Table("Datasheets_wargear.csv"),
            Table("Datasheets_unit_composition.csv"),
            Table("Datasheets_models_cost.csv"),
            Table("Datasheets_stratagems.csv"),
            Table("Datasheets_leader.csv"),
            Table("Stratagems.csv"),
            Table("Abilities.csv"),
            Table("Enhancements.csv"),
            Table("Detachment_abilities.csv"),
            Table("Detachments.csv"),
            Table("Detachments_chapter_dp.csv"),
            Table(LastUpdateFile).FirstOrDefault()?.Get("lastUpdate"));
    }
}
