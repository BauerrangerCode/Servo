using Servitor.RulesData.Domain;

namespace Servitor.App.Scoring.MissionDeck;

/// <summary>The Mission Deck 2026-27 content: every disposition pair's resolved Primary Mission, the Deployment cards and the Secondary Mission deck.</summary>
public sealed record MissionDeckData(IReadOnlyDictionary<(MissionArchetype Yours, MissionArchetype Opponent), PrimaryMission> PrimaryMissions, IReadOnlyList<DeploymentCard> DeploymentCards)
{
    public static readonly MissionDeckData Empty = new(new Dictionary<(MissionArchetype, MissionArchetype), PrimaryMission>(), []);

    public IReadOnlyList<SecondaryMission> SecondaryMissions { get; init; } = [];

    public SecondaryMission? FindSecondary(string name) => SecondaryMissions.FirstOrDefault(m => m.Name == name);

    public PrimaryMission? Resolve(MissionArchetype yours, MissionArchetype opponent) => PrimaryMissions.GetValueOrDefault((yours, opponent));
}

/// <summary>The result of reading the Mission Deck file: the parsed <see cref="Data"/> plus any entries that could not be read.</summary>
public sealed record MissionDeckSet(MissionDeckData Data, IReadOnlyList<string> Warnings)
{
    public static readonly MissionDeckSet Empty = new(MissionDeckData.Empty, []);
}
