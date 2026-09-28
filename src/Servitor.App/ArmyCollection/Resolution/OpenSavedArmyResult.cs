namespace Servitor.App.ArmyCollection.Resolution;

/// <summary>The result of resolving a <see cref="Servitor.App.ArmyCollection.State.SavedArmy"/> back into <see cref="Servitor.App.ArmyBuilder.State.IArmyBuilderState"/>.</summary>
public sealed record OpenSavedArmyResult(bool IsSuccess, IReadOnlyList<string> Errors)
{
    public static OpenSavedArmyResult Success { get; } = new(true, []);

    public static OpenSavedArmyResult Failure(IReadOnlyList<string> errors) => new(false, errors);
}
