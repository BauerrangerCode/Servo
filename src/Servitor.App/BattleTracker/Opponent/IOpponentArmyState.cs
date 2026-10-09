using Servitor.App.ArmyBuilder.State;
using Servitor.App.ArmyImport.Resolution;

namespace Servitor.App.BattleTracker.Opponent;

/// <summary>
/// The opponent's army (E6.3): a second, separately imported army with its own marks per unit. It is independent of the
/// player's own army (<see cref="IArmyBuilderState"/>) and of everything tracked for it. Held in memory only, like the
/// player's army.
/// </summary>
public interface IOpponentArmyState
{
    event Action? Changed;

    /// <summary>The opponent's army; empty until one was loaded.</summary>
    IArmyBuilderState Army { get; }

    bool HasArmy { get; }

    /// <summary>Parses and resolves a pasted Warscribe export into the opponent's army; a failed load leaves the army as it was.</summary>
    Task<ImportResolveResult> LoadAsync(string pastedText, CancellationToken cancellationToken = default);

    /// <summary>Removes the loaded army and every mark made on its units.</summary>
    void Clear();

    int Tokens(Guid unitId);

    void AddToken(Guid unitId);

    void RemoveToken(Guid unitId);

    bool IsDestroyed(Guid unitId);

    void ToggleDestroyed(Guid unitId);
}
