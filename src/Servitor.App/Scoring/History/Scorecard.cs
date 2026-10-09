using Servitor.App.Scoring.MissionDeck;
using Servitor.App.Scoring.State;
using Servitor.App.Scoring.Tracking;
using Servitor.RulesData.Domain;

namespace Servitor.App.Scoring.History;

public enum GameResult
{
    Victory,
    Defeat,
    Draw,
}

/// <summary>One side's final VP per round and per category, as capped by the Chapter Approved limits.</summary>
public sealed record SideScorecard(
    IReadOnlyList<int> PrimaryRounds,
    int PrimaryEndOfBattle,
    int Primary,
    SecondaryMode SecondaryMode,
    IReadOnlyList<int> SecondaryRounds,
    int Secondary,
    int Paint,
    int Challenger,
    int Total);

/// <summary>
/// The record of one finished game (E5.3), kept in the scorecard history. Everything is a plain value copied at the
/// moment the game ended, so a later rules-data update never changes a past game.
/// </summary>
public sealed record Scorecard(
    Guid Id,
    DateTimeOffset EndedAtUtc,
    MissionArchetype YourDisposition,
    MissionArchetype OpponentDisposition,
    string YourMission,
    string OpponentMission,
    string? Layout,
    BattleRole? Role,
    Side? FirstTurn,
    SideScorecard You,
    SideScorecard Enemy,
    GameResult Result);
