using Servitor.App.Scoring.MissionDeck;

namespace Servitor.App.Scoring.Tracking;

/// <summary>
/// The running VP of both sides (E5.2). Lives in a scoped service so the scorecard history (E5.3) and Play can read
/// it. Every entry is checked against the rules here — a condition outside its battle rounds, a third Fixed pick,
/// a second paid replacement — and a refused entry returns false and changes nothing.
/// </summary>
public interface IScoreTrackingState
{
    event Action? Changed;

    /// <summary>The battle round (1-5) entries are recorded in.</summary>
    int CurrentRound { get; }

    void SetRound(int round);

    PrimaryMission? MissionOf(Side side);

    SideScore ScoreOf(Side side);

    int PrimaryCount(Side side, int round, int conditionIndex);

    bool AdjustPrimary(Side side, int conditionIndex, int delta);

    void SetPainted(Side side, bool painted);

    void AdjustChallenger(Side side, int delta);

    SecondaryMode ModeOf(Side side);

    void SetMode(Side side, SecondaryMode mode);

    /// <summary>The Secondary Missions that can score for the side right now: the Fixed picks or the active Tactical cards.</summary>
    IReadOnlyList<SecondaryMission> ScoringCards(Side side);

    IReadOnlyList<SecondaryMission> FixedChoices { get; }

    IReadOnlyList<string> FixedPicks(Side side);

    bool ToggleFixedPick(Side side, string cardName);

    int SecondaryCount(Side side, string cardName, int round, int conditionIndex);

    bool AdjustSecondary(Side side, string cardName, int conditionIndex, int delta);

    int DeckRemaining(Side side);

    IReadOnlyList<string> DiscardedCards(Side side);

    bool ReplaceUsed(Side side);

    /// <summary>Draws the next card of the side's shuffled Tactical deck; null when the deck is empty.</summary>
    SecondaryMission? Draw(Side side);

    /// <summary>The cards still in the deck, for adding a specific card by hand.</summary>
    IReadOnlyList<SecondaryMission> DeckCards(Side side);

    bool AddCard(Side side, string cardName);

    /// <summary>Retires an active card (achieved, or discarded for 1CP); its VP entries stay counted.</summary>
    bool Discard(Side side, string cardName);

    /// <summary>The once-per-battle 1CP replacement: retires the card and draws a new one.</summary>
    bool Replace(Side side, string cardName);

    /// <summary>Clears every entry, e.g. for a new game.</summary>
    void Reset();
}
