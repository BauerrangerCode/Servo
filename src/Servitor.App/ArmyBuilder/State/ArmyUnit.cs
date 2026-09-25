using Servitor.RulesData.Domain;

namespace Servitor.App.ArmyBuilder.State;

/// <summary>A unit in the army list: the datasheet and the size (model count and points) it was added with.</summary>
public sealed record ArmyUnit(Guid Id, Datasheet Datasheet, UnitSize Size);
