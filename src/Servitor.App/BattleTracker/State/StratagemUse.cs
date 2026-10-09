using Servitor.App.Scoring.Tracking;

namespace Servitor.App.BattleTracker.State;

/// <summary>
/// One stratagem used by the player. Round, turn and phase say in which phase it counted, so a usage limit only looks
/// at the entries of the phase being played and nothing has to be cleared at a phase boundary.
/// </summary>
public sealed record StratagemUse(
    int Round,
    Side Turn,
    BattlePhase Phase,
    string StratagemId,
    string StratagemName,
    int CpCost,
    Guid? TargetUnitId);
