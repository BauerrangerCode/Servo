namespace Servitor.App.Scoring.MissionDeck;

/// <summary>
/// One of the Mission Deck's 6 named Deployment cards. Wahapedia gives no separate rules text per card — each
/// card is a map graphic — so the zone/objective descriptions here are read directly off that graphic (battlefield
/// edges, deployment zone shape, objective count and placement), not the exact reproduced GW card art; the visual
/// battlefield/terrain editor itself stays out of scope (decision D13).
/// </summary>
public sealed record DeploymentCard(string Name, string AttackerZone, string DefenderZone, string Objectives);
