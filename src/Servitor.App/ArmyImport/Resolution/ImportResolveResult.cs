namespace Servitor.App.ArmyImport.Resolution;

/// <summary>The result of resolving a <see cref="Parsing.ParsedArmyExport"/> against the rules-data catalog.</summary>
public sealed record ImportResolveResult(bool IsSuccess, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public static ImportResolveResult Success(IReadOnlyList<string> warnings) => new(true, [], warnings);

    public static ImportResolveResult Failure(IReadOnlyList<string> errors) => new(false, errors, []);
}
