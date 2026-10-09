namespace Servitor.App.Scoring.Tracking;

/// <summary>The Chapter Approved VP limits and round rules (Mission Deck 2026-27, "Determine Victor").</summary>
public static class ScoringRules
{
    public const int BattleRounds = 5;
    public const int PrimaryMaxVp = 45;
    public const int SecondaryMaxVp = 45;
    public const int MaxVpPerRound = 15;
    public const int FixedCardMaxVp = 20;
    public const int PaintVp = 10;
    public const int FixedPickCount = 2;
    public const int TacticalDrawCount = 2;

    private const string CountablePrefix = "For each";

    /// <summary>"For each X" conditions can score several times in one turn; every other condition is a yes/no.</summary>
    public static bool IsCountable(string condition) => condition.StartsWith(CountablePrefix, StringComparison.Ordinal);

    public static bool IsActiveIn(int firstRound, int lastRound, int round) => round >= firstRound && round <= lastRound;
}
