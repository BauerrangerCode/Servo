using Servitor.App.BattleTracker.State;
using Servitor.App.Scoring.Tracking;

namespace Servitor.App.BattleTracker.ActionQueue;

/// <summary>Where the game stands, as far as the timing of a rule is concerned.</summary>
public sealed record TurnContext(BattlePhase Phase, Side ActiveTurn, Side FirstTurn);
