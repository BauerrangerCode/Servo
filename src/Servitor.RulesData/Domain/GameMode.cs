namespace Servitor.RulesData.Domain;

/// <summary>
/// The game mode a stratagem belongs to. The rules-text source mixes the standard game with the Boarding Actions and
/// Challenger modes in the same files; only <see cref="Standard"/> is meant for the regular battle tracker.
/// </summary>
public enum GameMode
{
    Standard,
    BoardingActions,
    Challenger,
}
