using Servitor.App.BattleTracker.State;
using Servitor.App.Scoring.MissionDeck;
using Servitor.App.Scoring.State;
using Servitor.RulesData.Domain;

namespace Servitor.App.Scoring.Tracking;

public sealed class ScoreTrackingState : IScoreTrackingState
{
    private readonly IBattleSetupState _setup;
    private readonly IBattleProgressState _progress;
    private readonly IMissionDeckProvider _deckProvider;
    private readonly Random _random;
    private Dictionary<Side, ScoreSheet> _sheets = NewSheets();
    private (MissionArchetype? Yours, MissionArchetype? Opponent) _dispositions;

    public ScoreTrackingState(IBattleSetupState setup, IMissionDeckProvider deckProvider, IBattleProgressState progress, Random? random = null)
    {
        _setup = setup;
        _progress = progress;
        _deckProvider = deckProvider;
        _random = random ?? Random.Shared;
        _dispositions = (setup.YourDisposition, setup.OpponentDisposition);
        setup.Changed += OnSetupChanged;
        progress.Changed += () => Changed?.Invoke();
    }

    public event Action? Changed;

    public int CurrentRound => _progress.Round;

    private MissionDeckData Deck => _deckProvider.Current.Data;

    public IReadOnlyList<SecondaryMission> FixedChoices => Deck.SecondaryMissions.Where(m => m.FixedEligible).ToList();

    public Task SetRoundAsync(int round) => _progress.SetRoundAsync(round);

    public PrimaryMission? MissionOf(Side side) => side == Side.You ? _setup.ResolvedMission : _setup.OpponentMission;

    public SideScore ScoreOf(Side side) => VpCalculator.Calculate(MissionOf(side), Sheet(side), Deck);

    public int PrimaryCount(Side side, int round, int conditionIndex) => Sheet(side).PrimaryCounts.GetValueOrDefault((round, conditionIndex));

    public bool AdjustPrimary(Side side, int conditionIndex, int delta)
    {
        if (MissionOf(side) is not { } mission || conditionIndex < 0 || conditionIndex >= mission.ScoringConditions.Count)
        {
            return false;
        }

        var condition = mission.ScoringConditions[conditionIndex];
        return Adjust(Sheet(side).PrimaryCounts, (CurrentRound, conditionIndex), delta, condition.Condition, condition.FirstRound, condition.LastRound);
    }

    public void SetPainted(Side side, bool painted)
    {
        Sheet(side).Painted = painted;
        Changed?.Invoke();
    }

    public void AdjustChallenger(Side side, int delta)
    {
        var sheet = Sheet(side);
        sheet.Challenger = Math.Max(0, sheet.Challenger + delta);
        Changed?.Invoke();
    }

    public SecondaryMode ModeOf(Side side) => Sheet(side).Mode;

    public void SetMode(Side side, SecondaryMode mode)
    {
        Sheet(side).Mode = mode;
        Changed?.Invoke();
    }

    public IReadOnlyList<SecondaryMission> ScoringCards(Side side)
    {
        var sheet = Sheet(side);
        var names = sheet.Mode == SecondaryMode.Fixed ? sheet.FixedPicks : sheet.ActiveCards;
        return names.Select(Deck.FindSecondary).OfType<SecondaryMission>().ToList();
    }

    public IReadOnlyList<string> FixedPicks(Side side) => Sheet(side).FixedPicks;

    public bool ToggleFixedPick(Side side, string cardName)
    {
        var picks = Sheet(side).FixedPicks;
        if (Deck.FindSecondary(cardName) is not { FixedEligible: true })
        {
            return false;
        }

        if (!picks.Remove(cardName))
        {
            if (picks.Count >= ScoringRules.FixedPickCount)
            {
                return false;
            }

            picks.Add(cardName);
        }

        Changed?.Invoke();
        return true;
    }

    public int SecondaryCount(Side side, string cardName, int round, int conditionIndex) =>
        Sheet(side).SecondaryCounts.GetValueOrDefault((cardName, round, conditionIndex));

    public bool AdjustSecondary(Side side, string cardName, int conditionIndex, int delta)
    {
        var sheet = Sheet(side);
        var canScore = sheet.Mode == SecondaryMode.Fixed ? sheet.FixedPicks.Contains(cardName) : sheet.ActiveCards.Contains(cardName);
        if (!canScore || Deck.FindSecondary(cardName) is not { } card || conditionIndex < 0 || conditionIndex >= card.Conditions.Count)
        {
            return false;
        }

        var condition = card.Conditions[conditionIndex];
        return condition.PointsIn(sheet.Mode) is not null
            && Adjust(sheet.SecondaryCounts, (cardName, CurrentRound, conditionIndex), delta, condition.Condition, condition.FirstRound, condition.LastRound);
    }

    public int DeckRemaining(Side side) => Sheet(side) is { DeckBuilt: true } sheet ? sheet.Deck.Count : Deck.SecondaryMissions.Count;

    public IReadOnlyList<string> DiscardedCards(Side side) => Sheet(side).DiscardedCards;

    public bool ReplaceUsed(Side side) => Sheet(side).ReplaceUsed;

    public SecondaryMission? Draw(Side side)
    {
        var sheet = Sheet(side);
        EnsureDeck(sheet);
        if (sheet.Deck.Count == 0)
        {
            return null;
        }

        var name = sheet.Deck[0];
        sheet.Deck.RemoveAt(0);
        sheet.ActiveCards.Add(name);
        Changed?.Invoke();
        return Deck.FindSecondary(name);
    }

    public IReadOnlyList<SecondaryMission> DeckCards(Side side)
    {
        var sheet = Sheet(side);
        return sheet.DeckBuilt
            ? sheet.Deck.Select(Deck.FindSecondary).OfType<SecondaryMission>().ToList()
            : Deck.SecondaryMissions;
    }

    public bool AddCard(Side side, string cardName)
    {
        var sheet = Sheet(side);
        EnsureDeck(sheet);
        if (!sheet.Deck.Remove(cardName))
        {
            return false;
        }

        sheet.ActiveCards.Add(cardName);
        Changed?.Invoke();
        return true;
    }

    public bool Discard(Side side, string cardName)
    {
        var sheet = Sheet(side);
        if (!sheet.ActiveCards.Remove(cardName))
        {
            return false;
        }

        sheet.DiscardedCards.Add(cardName);
        Changed?.Invoke();
        return true;
    }

    public bool Replace(Side side, string cardName)
    {
        var sheet = Sheet(side);
        EnsureDeck(sheet);
        if (sheet.ReplaceUsed || sheet.Deck.Count == 0 || !sheet.ActiveCards.Contains(cardName))
        {
            return false;
        }

        sheet.ReplaceUsed = true;
        Discard(side, cardName);
        Draw(side);
        return true;
    }

    public void Reset()
    {
        _sheets = NewSheets();
        Changed?.Invoke();
    }

    private static Dictionary<Side, ScoreSheet> NewSheets() => new() { [Side.You] = new ScoreSheet(), [Side.Enemy] = new ScoreSheet() };

    private ScoreSheet Sheet(Side side) => _sheets[side];

    // The recorded entries belong to the two dispositions they were made under, so picking different ones starts clean.
    private void OnSetupChanged()
    {
        var dispositions = (_setup.YourDisposition, _setup.OpponentDisposition);
        if (dispositions != _dispositions)
        {
            _dispositions = dispositions;
            Reset();
        }
    }

    private void EnsureDeck(ScoreSheet sheet)
    {
        if (sheet.DeckBuilt)
        {
            return;
        }

        var names = Deck.SecondaryMissions.Select(m => m.Name).ToArray();
        _random.Shuffle(names);
        sheet.Deck.AddRange(names);
        sheet.DeckBuilt = true;
    }

    private bool Adjust<TKey>(Dictionary<TKey, int> counts, TKey key, int delta, string conditionText, int firstRound, int lastRound)
        where TKey : notnull
    {
        if (!ScoringRules.IsActiveIn(firstRound, lastRound, CurrentRound))
        {
            return false;
        }

        var next = counts.GetValueOrDefault(key) + delta;
        var max = ScoringRules.IsCountable(conditionText) ? int.MaxValue : 1;
        if (next < 0 || next > max)
        {
            return false;
        }

        if (next == 0)
        {
            counts.Remove(key);
        }
        else
        {
            counts[key] = next;
        }

        Changed?.Invoke();
        return true;
    }
}
