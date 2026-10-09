namespace Servitor.App.ArmyImport.Parsing;

/// <summary>The result of parsing pasted text: either a structured export, or the reasons it failed.</summary>
public sealed record ImportParseResult(ParsedArmyExport? Export, IReadOnlyList<string> Errors)
{
    public bool IsSuccess => Export is not null;

    public static ImportParseResult Success(ParsedArmyExport export) => new(export, []);

    public static ImportParseResult Failure(params string[] errors) => new(null, errors);

    public static ImportParseResult Failure(IReadOnlyList<string> errors) => new(null, errors);
}
