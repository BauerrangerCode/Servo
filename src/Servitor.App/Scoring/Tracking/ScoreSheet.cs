using Servitor.App.Scoring.MissionDeck;

namespace Servitor.App.Scoring.Tracking;

/// <summary>
/// One side's raw score entries: how often each scoring condition was achieved per round, plus the Secondary Mission
/// picks/deck. Only <see cref="ScoreTrackingState"/> changes it, because that is where the rules (round limits,
/// who may score what) are enforced; <see cref="VpCalculator"/> turns it into capped VP.
/// </summary>
public sealed class ScoreSheet
{
    /// <summary>Times each Primary condition (by index in the mission's list) was achieved in a round.</summary>
    public Dictionary<(int Round, int Index), int> PrimaryCounts { get; } = [];

    public SecondaryMode Mode { get; set; } = SecondaryMode.Tactical;

    public List<string> FixedPicks { get; } = [];

    /// <summary>Tactical cards still in the deck, in draw order; filled when the deck is first needed.</summary>
    public List<string> Deck { get; } = [];

    public bool DeckBuilt { get; set; }

    public List<string> ActiveCards { get; } = [];

    public List<string> DiscardedCards { get; } = [];

    /// <summary>The once-per-battle 1CP "discard and draw a replacement" has been spent.</summary>
    public bool ReplaceUsed { get; set; }

    /// <summary>Times each Secondary condition (by card name and index) was achieved in a round.</summary>
    public Dictionary<(string Card, int Round, int Index), int> SecondaryCounts { get; } = [];

    /// <summary>The 10VP Battle Ready (painted army) bonus.</summary>
    public bool Painted { get; set; }

    /// <summary>The manually counted Challenger bonus.</summary>
    public int Challenger { get; set; }
}
