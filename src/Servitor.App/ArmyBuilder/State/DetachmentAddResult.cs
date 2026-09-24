namespace Servitor.App.ArmyBuilder.State;

public enum DetachmentAddResult
{
    Added,
    NoBattleSize,
    NoFaction,
    NotStandardGame,
    AlreadySelected,
    ExceedsDetachmentPoints,
}
