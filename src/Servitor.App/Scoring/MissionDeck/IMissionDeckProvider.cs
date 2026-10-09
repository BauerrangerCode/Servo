namespace Servitor.App.Scoring.MissionDeck;

/// <summary>Holds the Mission Deck data of the deck file for as long as the app runs.</summary>
public interface IMissionDeckProvider
{
    /// <summary>The deck loaded so far; empty until <see cref="LoadAsync"/> has run.</summary>
    MissionDeckSet Current { get; }

    /// <summary>Reads the deck file again, so a changed file is picked up without restarting the app.</summary>
    Task LoadAsync(CancellationToken cancellationToken = default);
}
