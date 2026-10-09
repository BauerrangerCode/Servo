using Servitor.App.Scoring.Tracking;

namespace Servitor.App.BattleTracker.State;

/// <summary>The persisted part of the battle progress.</summary>
public sealed record BattleProgress(int Round, BattlePhase Phase, Side FirstTurn, Side ActiveTurn);
